using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace MARIS.Rendering.InstanceRendering.Corals
{
    /// <summary>How a coral group's per-cell density is derived from its density mask.</summary>
    public enum DensityMode
    {
        /// <summary>Sample the mask once per cell and place a proportional, uniformly scattered count. Cheapest, but density is blocky at cell resolution.</summary>
        CountBased = 0,
        /// <summary>Sample the mask per candidate instance and accept by probability. Captures fine mask detail at a higher per-instance cost.</summary>
        Stochastic = 1
    }

    /// <summary>
    /// Streams and renders coral instances around the player camera. The world is divided
    /// into a regular grid of cells on the XZ plane. Each cell deterministically generates
    /// the same coral layout every time it is visited, because its layout is derived purely
    /// from its grid coordinates and the world seed. As a result coral stays in place as the
    /// player roams away and returns, which makes the seabed feel populated everywhere while
    /// only a small area is ever resident in memory.
    ///
    /// A disc of cells within <see cref="viewDistance"/> of the player is kept generated
    /// ("active") so turning around is free, while only the cells inside the camera frustum
    /// are actually drawn. Every cell of a group shares one material and
    /// <see cref="MaterialPropertyBlock"/>, so a whole group renders in just a few instanced
    /// draw calls via <see cref="Graphics.RenderMeshInstanced"/> (WebGL 2.0 compatible).
    /// </summary>
    public class CoralInstanceManager : MonoBehaviour
    {
        // RenderMeshInstanced uploads the matrices through a constant buffer, which on most
        // platforms (including WebGL 2.0) caps a single instanced batch at 1023 instances.
        private const int MaxInstancesPerBatch = 1023;

        [Header("Shared Rendering")]
        [Tooltip("Material shared by every coral group. Must have 'Enable GPU Instancing' checked and a shader that reads the per-group properties.")]
        [SerializeField] private Material material;

        [Tooltip("Mesh used by a group that does not define its own mesh.")]
        [SerializeField] private Mesh sharedMesh;

        [Header("Player / Streaming")]
        [Tooltip("Camera the coral field is centered on and culled against. Falls back to Camera.main when empty.")]
        [SerializeField] private Camera playerCamera;

        [Tooltip("Size (meters) of one world cell on the XZ plane. Smaller cells stream in finer steps.")]
        [Min(1f)][SerializeField] private float cellSize = 16f;

        [Tooltip("How far (meters) around the player cells are kept generated and rendered.")]
        [Min(1f)][SerializeField] private float viewDistance = 120f;

        [Tooltip("Maximum number of cells generated per frame. Spreading generation across frames keeps WebGL " +
                 "smooth when many cells enter the view at once (terrain height/slope sampling is the main cost).")]
        [Min(1)][SerializeField] private int generationBudgetPerFrame = 4;

        [Tooltip("Seed mixed into every cell's hash. Change it for a completely different world layout.")]
        [SerializeField] private int worldSeed = 12345;

        [Tooltip("Extra margin (meters) added to the frustum test so corals do not pop right at the screen edge.")]
        [Min(0f)][SerializeField] private float cullPadding = 4f;

        [Header("Terrain Placement")]
        [Tooltip("Terrain corals are placed on. When empty, corals sit on a flat plane at Height Offset and density masks are ignored.")]
        [SerializeField] private Terrain terrain;

        [Tooltip("Vertical offset (meters) added on top of the sampled terrain height. Use it to sink or lift coral relative to the seabed.")]
        [SerializeField] private float heightOffset = 0f;

        [Header("Density Masks")]
        [Tooltip("Density mask A. Mapped across the terrain footprint; a group's channel value scales how many corals spawn.")]
        [SerializeField] private Texture2D densityMaskA;

        [Tooltip("Density mask B. Optional second mask for more species.")]
        [SerializeField] private Texture2D densityMaskB;

        [Tooltip("Count-based: sample the mask once per cell and place a proportional count (cheapest, blocky). " +
                 "Stochastic: sample the mask per instance and accept by probability (per-instance detail, costs more). " +
                 "Toggle to compare quality and performance.")]
        [SerializeField] private DensityMode densityMode = DensityMode.Stochastic;

        [Header("Shadows")]
        [SerializeField] private ShadowCastingMode shadowCastingMode = ShadowCastingMode.On;
        [SerializeField] private bool receiveShadows = true;

        [Header("Coral Groups")]
        [SerializeField] private List<CoralRenderGroup> groups = new List<CoralRenderGroup>();

        // Cached property ids. These match SetCoralMaterialProperties.cs and the coral shader.
        private static readonly int MainTexIndexId = Shader.PropertyToID("_MainTexIndex");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int WaveIntensityId = Shader.PropertyToID("_WaveIntensity");
        private static readonly int BendOffsetId = Shader.PropertyToID("_BendOffset");
        private static readonly int VerticalBendStrengthId = Shader.PropertyToID("_VerticalBendStrength");
        private static readonly int ColorMultiplierId = Shader.PropertyToID("_ColorMultiplier");

        private readonly List<GroupRuntime> groupRuntimes = new List<GroupRuntime>();
        private readonly Dictionary<long, CoralCell> activeCells = new Dictionary<long, CoralCell>();
        private readonly Stack<CoralCell> cellPool = new Stack<CoralCell>();
        private readonly List<long> removalScratch = new List<long>();
        private readonly Plane[] frustumPlanes = new Plane[6];

        // Streaming is spread across frames: desiredCells is the disc we want resident, and
        // generationQueue holds the not-yet-built cells (nearest first) drained at a fixed budget.
        private readonly HashSet<long> desiredCells = new HashSet<long>();
        private readonly List<PendingCell> pendingScratch = new List<PendingCell>();
        private readonly List<long> generationQueue = new List<long>();
        private int genQueueIndex;

        // Cached, CPU-readable density masks and terrain footprint so per-instance sampling stays cheap.
        private DensityMaskData maskA;
        private DensityMaskData maskB;
        private TerrainData terrainData;
        private bool hasTerrain;
        private float terrainOriginX, terrainOriginY, terrainOriginZ;
        private float terrainSizeX, terrainSizeZ;

        private bool hasPlayerCell;
        private int playerCellX, playerCellZ;

        [ShowInInspector, ReadOnly] public int ActiveCellCount => activeCells.Count;
        [ShowInInspector, ReadOnly] public int PendingCellCount => Mathf.Max(0, generationQueue.Count - genQueueIndex);
        [ShowInInspector, ReadOnly] public int RenderedInstances { get; private set; }
        [ShowInInspector, ReadOnly] public int DrawBatches { get; private set; }

        private void Start()
        {
            RebuildGroupRuntimes();
        }

        private void Update()
        {
            if (material == null || groupRuntimes.Count == 0)
                return;

            Camera camera = playerCamera != null ? playerCamera : Camera.main;
            if (camera == null)
                return;

            UpdateActiveCells(camera.transform.position);
            ProcessGenerationQueue();
            RenderVisibleCells(camera);
        }

        /// <summary>Rebuilds per-group state and drops every generated cell. Call after changing groups or the material.</summary>
        [Button("Rebuild")]
        public void Rebuild()
        {
            RebuildGroupRuntimes();
        }

        /// <summary>Drops every generated cell. They regenerate deterministically on the next update.</summary>
        [Button("Clear")]
        public void Clear()
        {
            ReleaseAllCells();
            desiredCells.Clear();
            generationQueue.Clear();
            genQueueIndex = 0;
            hasPlayerCell = false;
            RenderedInstances = 0;
            DrawBatches = 0;
        }

        /// <summary>
        /// Sets the vitality [0,1] for every group that uses the given species key. Vitality is applied at
        /// draw time from each instance's stored threshold, so cells are not regenerated and the original
        /// corals reappear exactly when vitality returns to 1. A value of 1 leaves every coral healthy; a
        /// lower value affects a random ~(1 - vitality) fraction (Disappear hides them, Bleach swaps texture).
        /// </summary>
        public void SetSpeciesVitality(string speciesKey, float vitality)
        {
            if (string.IsNullOrEmpty(speciesKey))
                return;

            vitality = Mathf.Clamp01(vitality);

            for (int g = 0; g < groupRuntimes.Count; g++)
            {
                CoralRenderGroup group = groups[groupRuntimes[g].sourceIndex];
                if (group.speciesKey == speciesKey)
                    groupRuntimes[g].vitality = vitality;
            }
        }

        private void RebuildGroupRuntimes()
        {
            groupRuntimes.Clear();
            activeCells.Clear();
            cellPool.Clear();
            desiredCells.Clear();
            generationQueue.Clear();
            genQueueIndex = 0;
            hasPlayerCell = false;
            RenderedInstances = 0;
            DrawBatches = 0;

            RefreshTerrainAndMasks();

            if (material == null)
            {
                Debug.LogWarning("[CoralInstanceManager] No shared material assigned; nothing will be rendered.", this);
                return;
            }

            if (!material.enableInstancing)
                Debug.LogWarning("[CoralInstanceManager] The shared material does not have 'Enable GPU Instancing' enabled; instanced rendering will not batch correctly.", this);

            for (int i = 0; i < groups.Count; i++)
            {
                CoralRenderGroup group = groups[i];
                if (group == null)
                    continue;

                Mesh mesh = group.mesh != null ? group.mesh : sharedMesh;
                if (mesh == null)
                {
                    Debug.LogWarning($"[CoralInstanceManager] Group '{group.name}' has no mesh and no shared mesh is set; skipping.", this);
                    continue;
                }

                groupRuntimes.Add(new GroupRuntime
                {
                    sourceIndex = i,
                    mesh = mesh,
                    response = group.vitalityResponse,
                    vitality = 1f,
                    propertyBlock = BuildPropertyBlock(group, group.atlasIndex),
                    bleachBlock = BuildPropertyBlock(group, group.atlasIndex + 1, 0f)
                });
            }
        }

        private void UpdateActiveCells(Vector3 playerPosition)
        {
            int centerX = Mathf.FloorToInt(playerPosition.x / cellSize);
            int centerZ = Mathf.FloorToInt(playerPosition.z / cellSize);

            // The active disc only changes when the player crosses into a new cell.
            if (hasPlayerCell && centerX == playerCellX && centerZ == playerCellZ)
                return;

            playerCellX = centerX;
            playerCellZ = centerZ;
            hasPlayerCell = true;

            int cellRadius = Mathf.CeilToInt(viewDistance / cellSize);
            float addDistanceSqr = viewDistance * viewDistance;

            // Recompute the disc of cells we want resident and queue the missing ones (nearest first).
            // Generation is spread across frames by ProcessGenerationQueue to avoid WebGL hitches.
            desiredCells.Clear();
            pendingScratch.Clear();

            for (int dz = -cellRadius; dz <= cellRadius; dz++)
            {
                for (int dx = -cellRadius; dx <= cellRadius; dx++)
                {
                    int cx = centerX + dx;
                    int cz = centerZ + dz;

                    float distSqr = CellCenterDistanceSqr(cx, cz, playerPosition);
                    if (distSqr > addDistanceSqr)
                        continue;

                    long key = PackCell(cx, cz);
                    desiredCells.Add(key);

                    if (!activeCells.ContainsKey(key))
                        pendingScratch.Add(new PendingCell(key, cx, cz, distSqr));
                }
            }

            pendingScratch.Sort((a, b) => a.distanceSqr.CompareTo(b.distanceSqr));
            generationQueue.Clear();
            genQueueIndex = 0;
            for (int i = 0; i < pendingScratch.Count; i++)
                generationQueue.Add(pendingScratch[i].key);

            // Release cells that fell outside the disc (one cell of hysteresis avoids churn).
            float removeDistanceSqr = (viewDistance + cellSize) * (viewDistance + cellSize);
            removalScratch.Clear();
            foreach (KeyValuePair<long, CoralCell> kvp in activeCells)
            {
                if (CellCenterDistanceSqr(kvp.Value.x, kvp.Value.z, playerPosition) > removeDistanceSqr)
                    removalScratch.Add(kvp.Key);
            }

            for (int i = 0; i < removalScratch.Count; i++)
            {
                long key = removalScratch[i];
                cellPool.Push(activeCells[key]);
                activeCells.Remove(key);
            }
        }

        /// <summary>Generates at most <see cref="generationBudgetPerFrame"/> queued cells per frame to keep WebGL smooth.</summary>
        private void ProcessGenerationQueue()
        {
            int budget = generationBudgetPerFrame;
            while (budget > 0 && genQueueIndex < generationQueue.Count)
            {
                long key = generationQueue[genQueueIndex++];

                // Skip cells that were released again or already built since they were queued.
                if (!desiredCells.Contains(key) || activeCells.ContainsKey(key))
                    continue;

                int cx = (int)(key >> 32);
                int cz = (int)(key & 0xFFFFFFFF);
                activeCells.Add(key, GenerateCell(cx, cz));
                budget--;
            }

            if (genQueueIndex >= generationQueue.Count)
            {
                generationQueue.Clear();
                genQueueIndex = 0;
            }
        }

        private void RenderVisibleCells(Camera camera)
        {
            GeometryUtility.CalculateFrustumPlanes(camera, frustumPlanes);

            for (int g = 0; g < groupRuntimes.Count; g++)
            {
                groupRuntimes[g].drawCount = 0;
                groupRuntimes[g].bleachCount = 0;
            }

            // Gather only the cells the camera can actually see into per-group draw buffers.
            foreach (KeyValuePair<long, CoralCell> kvp in activeCells)
            {
                CoralCell cell = kvp.Value;

                Bounds bounds = cell.bounds;
                if (cullPadding > 0f)
                    bounds.Expand(cullPadding * 2f);

                if (!GeometryUtility.TestPlanesAABB(frustumPlanes, bounds))
                    continue;

                for (int g = 0; g < groupRuntimes.Count; g++)
                {
                    int used = cell.usedPerGroup[g];
                    if (used == 0)
                        continue;

                    Matrix4x4[] source = cell.matricesPerGroup[g];
                    if (source == null)
                        continue;

                    GroupRuntime runtime = groupRuntimes[g];

                    // Full vitality: every coral is healthy, so copy the whole cell in one block.
                    if (runtime.vitality >= 1f)
                    {
                        EnsureCapacity(runtime, runtime.drawCount + used);
                        Array.Copy(source, 0, runtime.drawArray, runtime.drawCount, used);
                        runtime.drawCount += used;
                        continue;
                    }

                    // Below full vitality: split per instance. An instance is affected when its stable
                    // threshold is greater than the current vitality, giving a random ~(1 - vitality) share.
                    float[] thresholds = cell.thresholdsPerGroup[g];
                    bool bleach = runtime.response == VitalityResponse.Bleach;

                    for (int i = 0; i < used; i++)
                    {
                        bool affected = thresholds[i] > runtime.vitality;

                        if (!affected)
                        {
                            EnsureCapacity(runtime, runtime.drawCount + 1);
                            runtime.drawArray[runtime.drawCount++] = source[i];
                        }
                        else if (bleach)
                        {
                            // Disappearing corals draw nothing; bleached corals draw with the bleach block.
                            EnsureBleachCapacity(runtime, runtime.bleachCount + 1);
                            runtime.bleachArray[runtime.bleachCount++] = source[i];
                        }
                    }
                }
            }

            int totalInstances = 0;
            int totalBatches = 0;
            Bounds renderBounds = new Bounds(camera.transform.position, Vector3.one * (viewDistance * 2f + 100f));

            for (int g = 0; g < groupRuntimes.Count; g++)
            {
                GroupRuntime runtime = groupRuntimes[g];
                if (runtime.drawCount == 0 && runtime.bleachCount == 0)
                    continue;

                if (runtime.drawCount > 0)
                {
                    RenderParams renderParams = new RenderParams(material)
                    {
                        worldBounds = renderBounds,
                        matProps = runtime.propertyBlock,
                        shadowCastingMode = shadowCastingMode,
                        receiveShadows = receiveShadows,
                        layer = gameObject.layer
                    };

                    for (int start = 0; start < runtime.drawCount; start += MaxInstancesPerBatch)
                    {
                        int batchCount = Mathf.Min(MaxInstancesPerBatch, runtime.drawCount - start);
                        Graphics.RenderMeshInstanced(renderParams, runtime.mesh, 0, runtime.drawArray, batchCount, start);
                        totalBatches++;
                    }

                    totalInstances += runtime.drawCount;
                }

                if (runtime.bleachCount > 0)
                {
                    RenderParams bleachParams = new RenderParams(material)
                    {
                        worldBounds = renderBounds,
                        matProps = runtime.bleachBlock,
                        shadowCastingMode = shadowCastingMode,
                        receiveShadows = receiveShadows,
                        layer = gameObject.layer
                    };

                    for (int start = 0; start < runtime.bleachCount; start += MaxInstancesPerBatch)
                    {
                        int batchCount = Mathf.Min(MaxInstancesPerBatch, runtime.bleachCount - start);
                        Graphics.RenderMeshInstanced(bleachParams, runtime.mesh, 0, runtime.bleachArray, batchCount, start);
                        totalBatches++;
                    }

                    totalInstances += runtime.bleachCount;
                }
            }

            RenderedInstances = totalInstances;
            DrawBatches = totalBatches;
        }

        private CoralCell GenerateCell(int cellX, int cellZ)
        {
            CoralCell cell = cellPool.Count > 0 ? cellPool.Pop() : new CoralCell();
            cell.x = cellX;
            cell.z = cellZ;

            int groupCount = groupRuntimes.Count;
            if (cell.matricesPerGroup == null || cell.matricesPerGroup.Length != groupCount)
            {
                cell.matricesPerGroup = new Matrix4x4[groupCount][];
                cell.usedPerGroup = new int[groupCount];
                cell.thresholdsPerGroup = new float[groupCount][];
            }

            float originX = cellX * cellSize;
            float originZ = cellZ * cellSize;

            // Track the real vertical extent so the frustum bounds hug the terrain instead of a fixed slab.
            float minY = float.MaxValue;
            float maxY = float.MinValue;

            for (int g = 0; g < groupCount; g++)
            {
                CoralRenderGroup group = groups[groupRuntimes[g].sourceIndex];
                int maxCount = Mathf.Max(0, group.instancesPerCell);

                Matrix4x4[] matrices = cell.matricesPerGroup[g];
                if (matrices == null || matrices.Length != maxCount)
                {
                    matrices = new Matrix4x4[maxCount];
                    cell.matricesPerGroup[g] = matrices;
                }

                float[] thresholds = cell.thresholdsPerGroup[g];
                if (thresholds == null || thresholds.Length != maxCount)
                {
                    thresholds = new float[maxCount];
                    cell.thresholdsPerGroup[g] = thresholds;
                }

                // Layout depends only on the cell coordinates, the group and the world seed,
                // so a given world location always produces the exact same corals.
                uint seed = Hash((uint)cellX, (uint)cellZ, (uint)groupRuntimes[g].sourceIndex, (uint)worldSeed);
                DeterministicRng rng = new DeterministicRng(seed);

                float minScale = Mathf.Min(group.minScale, group.maxScale);
                float maxScale = Mathf.Max(group.minScale, group.maxScale);
                bool usesMask = group.densityMaskIndex >= 0 && hasTerrain;

                // Count-based mode samples the mask once at the cell center and scales the candidate
                // count, so the per-instance mask cost disappears. Stochastic mode keeps all candidates
                // and rejects each by its local mask value, capturing fine detail at a higher cost.
                int candidateCount = maxCount;
                if (usesMask && densityMode == DensityMode.CountBased)
                {
                    float centerDensity = SampleDensity(group, originX + cellSize * 0.5f, originZ + cellSize * 0.5f);
                    candidateCount = centerDensity < group.densityCutoff ? 0 : Mathf.RoundToInt(maxCount * centerDensity);
                }

                int used = 0;
                for (int i = 0; i < candidateCount; i++)
                {
                    float px = originX + rng.NextFloat() * cellSize;
                    float pz = originZ + rng.NextFloat() * cellSize;
                    float yaw = rng.NextFloat() * 360f;
                    float scale = Mathf.Lerp(minScale, maxScale, rng.NextFloat());

                    // Stochastic rejection: accept this candidate with probability = local mask value.
                    if (usesMask && densityMode == DensityMode.Stochastic)
                    {
                        float density = SampleDensity(group, px, pz);
                        if (density < group.densityCutoff || rng.NextFloat() > density)
                            continue;
                    }

                    SampleTerrain(px, pz, out float py, out Vector3 normal);

                    // Tilt toward the surface normal by a per-instance, randomized fraction so corals on the
                    // same slope do not all lean identically, then apply the random yaw around up.
                    float influence = Mathf.Clamp01(group.slopeInfluence + (rng.NextFloat() * 2f - 1f) * group.slopeRandomness);
                    Quaternion align = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, normal), influence);
                    Quaternion rotation = align * Quaternion.Euler(0f, yaw, 0f);

                    // Sink the coral along the surface normal so bases with different pivot heights all sit
                    // flush on the seabed. The random component adds extra depth only (never lifts), giving
                    // variation without any instance appearing to float.
                    float sinkDepth = (group.normalOffset + rng.NextFloat() * group.normalOffsetRandomness) * scale;
                    Vector3 position = new Vector3(px, py, pz) - normal * sinkDepth;

                    // Stable per-instance vitality threshold. Drawn last so it does not shift the layout of
                    // existing corals. The instance is affected (bleached or hidden) when this threshold is
                    // greater than the group's current vitality, giving a random ~(1 - vitality) fraction.
                    thresholds[used] = rng.NextFloat();
                    matrices[used++] = Matrix4x4.TRS(position, rotation, new Vector3(scale, scale, scale));

                    if (position.y < minY) minY = position.y;
                    if (position.y > maxY) maxY = position.y;
                }

                cell.usedPerGroup[g] = used;
            }

            // Build bounds from the real terrain extent (with a margin for coral height); fall back to a
            // thin slab at the height offset when the cell ended up empty.
            float centerX = originX + cellSize * 0.5f;
            float centerZ = originZ + cellSize * 0.5f;
            if (maxY < minY)
            {
                minY = maxY = (hasTerrain ? terrainOriginY : 0f) + heightOffset;
            }

            const float verticalMargin = 8f;
            float centerY = (minY + maxY) * 0.5f;
            float sizeY = (maxY - minY) + verticalMargin * 2f;
            cell.bounds = new Bounds(new Vector3(centerX, centerY, centerZ), new Vector3(cellSize, sizeY, cellSize));
            return cell;
        }

        private MaterialPropertyBlock BuildPropertyBlock(CoralRenderGroup group, int atlasIndex, float waveIntensityOverride = -1f)
        {
            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            mpb.SetFloat(MainTexIndexId, atlasIndex);
            mpb.SetFloat(SmoothnessId, group.smoothness);
            mpb.SetFloat(MetallicId, group.metallic);
            mpb.SetFloat(WaveIntensityId, waveIntensityOverride >= 0f ? waveIntensityOverride : group.waveIntensity);
            mpb.SetFloat(BendOffsetId, group.bendOffset);
            mpb.SetFloat(VerticalBendStrengthId, group.verticalBendStrength);
            mpb.SetColor(ColorMultiplierId, group.colorMultiplier);
            return mpb;
        }

        /// <summary>
        /// Caches the terrain footprint and snapshots each density mask into a CPU array via
        /// <see cref="Texture2D.GetPixels32"/>. Sampling the cached arrays per instance avoids the
        /// per-pixel cost (and GPU readback) of <see cref="Texture2D.GetPixelBilinear"/> on WebGL.
        /// </summary>
        private void RefreshTerrainAndMasks()
        {
            hasTerrain = terrain != null && terrain.terrainData != null;
            if (hasTerrain)
            {
                terrainData = terrain.terrainData;
                Vector3 pos = terrain.GetPosition();
                Vector3 size = terrainData.size;
                terrainOriginX = pos.x;
                terrainOriginY = pos.y;
                terrainOriginZ = pos.z;
                terrainSizeX = size.x;
                terrainSizeZ = size.z;
            }
            else
            {
                terrainData = null;
            }

            maskA = DensityMaskData.Create(densityMaskA, this);
            maskB = DensityMaskData.Create(densityMaskB, this);
        }

        /// <summary>Returns the density [0,1] for a group at a world XZ position, or 1 when the group uses no mask.</summary>
        private float SampleDensity(CoralRenderGroup group, float worldX, float worldZ)
        {
            if (group.densityMaskIndex < 0 || !hasTerrain)
                return 1f;

            DensityMaskData mask = group.densityMaskIndex == 0 ? maskA : maskB;
            if (!mask.IsValid)
                return 1f;

            // Terrain footprint maps directly onto the mask's UV space.
            float u = (worldX - terrainOriginX) / terrainSizeX;
            float v = (worldZ - terrainOriginZ) / terrainSizeZ;
            return mask.SampleBilinear(u, v, group.densityChannel);
        }

        /// <summary>
        /// Samples per-instance terrain height and surface normal at a world XZ position.
        /// Height comes from <see cref="Terrain.SampleHeight"/> (bilinear over the heightmap) and the
        /// normal from <see cref="TerrainData.GetInterpolatedNormal"/>, so steep, detailed slopes are
        /// captured per instance rather than per cell. Falls back to a flat plane when no terrain is set.
        /// </summary>
        private void SampleTerrain(float worldX, float worldZ, out float worldY, out Vector3 normal)
        {
            if (!hasTerrain)
            {
                worldY = heightOffset;
                normal = Vector3.up;
                return;
            }

            worldY = terrainOriginY + terrain.SampleHeight(new Vector3(worldX, 0f, worldZ)) + heightOffset;

            float u = Mathf.Clamp01((worldX - terrainOriginX) / terrainSizeX);
            float v = Mathf.Clamp01((worldZ - terrainOriginZ) / terrainSizeZ);
            normal = terrainData.GetInterpolatedNormal(u, v);
        }

        private void ReleaseAllCells()
        {
            foreach (KeyValuePair<long, CoralCell> kvp in activeCells)
                cellPool.Push(kvp.Value);
            activeCells.Clear();
        }

        private float CellCenterDistanceSqr(int cellX, int cellZ, Vector3 playerPosition)
        {
            float dx = (cellX + 0.5f) * cellSize - playerPosition.x;
            float dz = (cellZ + 0.5f) * cellSize - playerPosition.z;
            return dx * dx + dz * dz;
        }

        private static void EnsureCapacity(GroupRuntime runtime, int required)
        {
            if (runtime.drawArray.Length >= required)
                return;

            int newSize = Mathf.Max(runtime.drawArray.Length * 2, required);
            Matrix4x4[] grown = new Matrix4x4[newSize];
            Array.Copy(runtime.drawArray, grown, runtime.drawCount);
            runtime.drawArray = grown;
        }

        private static void EnsureBleachCapacity(GroupRuntime runtime, int required)
        {
            if (runtime.bleachArray.Length >= required)
                return;

            int newSize = Mathf.Max(runtime.bleachArray.Length * 2, required);
            Matrix4x4[] grown = new Matrix4x4[newSize];
            Array.Copy(runtime.bleachArray, grown, runtime.bleachCount);
            runtime.bleachArray = grown;
        }

        private static long PackCell(int x, int z)
        {
            return ((long)x << 32) | (uint)z;
        }

        private static uint Hash(uint x, uint y, uint z, uint seed)
        {
            unchecked
            {
                uint h = seed + 0x9E3779B9u;
                h ^= x; h *= 0x85EBCA77u; h ^= h >> 15;
                h ^= y; h *= 0xC2B2AE3Du; h ^= h >> 13;
                h ^= z; h *= 0x27D4EB2Fu; h ^= h >> 16;
                return h;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Camera camera = playerCamera != null ? playerCamera : Camera.main;
            Vector3 center = camera != null ? camera.transform.position : transform.position;
            float ringHeight = (terrain != null ? terrain.GetPosition().y : 0f) + heightOffset;

            Gizmos.color = new Color(0f, 1f, 1f, 0.35f);
            Gizmos.DrawWireSphere(new Vector3(center.x, ringHeight, center.z), viewDistance);

            Gizmos.color = new Color(0f, 1f, 0.5f, 0.15f);
            foreach (KeyValuePair<long, CoralCell> kvp in activeCells)
                Gizmos.DrawWireCube(kvp.Value.bounds.center, kvp.Value.bounds.size);
        }

        private struct DeterministicRng
        {
            private uint state;

            public DeterministicRng(uint seed)
            {
                state = seed == 0u ? 1u : seed;
            }

            public float NextFloat()
            {
                // xorshift32, returns a value in [0, 1).
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return (state & 0x00FFFFFFu) / 16777216f;
            }
        }

        /// <summary>A density mask snapshotted into a CPU array once, sampled with managed bilinear filtering.</summary>
        private readonly struct DensityMaskData
        {
            private readonly Color32[] pixels;
            private readonly int width;
            private readonly int height;

            public bool IsValid => pixels != null;

            private DensityMaskData(Color32[] pixels, int width, int height)
            {
                this.pixels = pixels;
                this.width = width;
                this.height = height;
            }

            public static DensityMaskData Create(Texture2D texture, UnityEngine.Object context)
            {
                if (texture == null)
                    return default;

                if (!texture.isReadable)
                {
                    Debug.LogWarning($"[CoralInstanceManager] Density mask '{texture.name}' is not readable; " +
                                     "enable Read/Write in its import settings. Treating its groups as full density.", context);
                    return default;
                }

                return new DensityMaskData(texture.GetPixels32(), texture.width, texture.height);
            }

            /// <summary>Bilinearly samples one channel at UV (clamped) and returns it in [0,1].</summary>
            public float SampleBilinear(float u, float v, DensityChannel channel)
            {
                if (pixels == null)
                    return 1f;

                // Clamp into the valid texel-center range.
                float fx = Mathf.Clamp01(u) * (width - 1);
                float fy = Mathf.Clamp01(v) * (height - 1);

                int x0 = (int)fx;
                int y0 = (int)fy;
                int x1 = x0 + 1 < width ? x0 + 1 : x0;
                int y1 = y0 + 1 < height ? y0 + 1 : y0;
                float tx = fx - x0;
                float ty = fy - y0;

                float c00 = Channel(pixels[y0 * width + x0], channel);
                float c10 = Channel(pixels[y0 * width + x1], channel);
                float c01 = Channel(pixels[y1 * width + x0], channel);
                float c11 = Channel(pixels[y1 * width + x1], channel);

                float top = c00 + (c10 - c00) * tx;
                float bottom = c01 + (c11 - c01) * tx;
                return (top + (bottom - top) * ty) / 255f;
            }

            private static byte Channel(Color32 c, DensityChannel channel)
            {
                switch (channel)
                {
                    case DensityChannel.Green: return c.g;
                    case DensityChannel.Blue: return c.b;
                    case DensityChannel.Alpha: return c.a;
                    default: return c.r;
                }
            }
        }

        private class GroupRuntime
        {
            public int sourceIndex;
            public Mesh mesh;

            // Vitality in [0,1]. 1 = every coral healthy. An instance is "affected" when its stored
            // per-instance threshold is greater than this value, so the same corals stay affected as
            // vitality changes and originals restore exactly when vitality returns to 1.
            public float vitality = 1f;
            public VitalityResponse response = VitalityResponse.Disappear;

            // Healthy instances draw with propertyBlock (_MainTexIndex = atlasIndex). Bleached instances
            // draw with bleachBlock (_MainTexIndex = atlasIndex + 1). Disappearing instances draw nothing.
            public MaterialPropertyBlock propertyBlock;
            public MaterialPropertyBlock bleachBlock;

            public Matrix4x4[] drawArray = new Matrix4x4[1024];
            public int drawCount;

            // Second buffer for affected instances (only used by the Bleach response).
            public Matrix4x4[] bleachArray = new Matrix4x4[1024];
            public int bleachCount;
        }

        // A cell that is wanted but not yet generated, tagged with its distance so the queue builds nearest first.
        private readonly struct PendingCell
        {
            public readonly long key;
            public readonly int x;
            public readonly int z;
            public readonly float distanceSqr;

            public PendingCell(long key, int x, int z, float distanceSqr)
            {
                this.key = key;
                this.x = x;
                this.z = z;
                this.distanceSqr = distanceSqr;
            }
        }

        private class CoralCell
        {
            public int x;
            public int z;
            public Bounds bounds;

            // matricesPerGroup[g] is sized to the group's max candidates and reused across cells;
            // usedPerGroup[g] is how many entries are actually populated (density varies per cell).
            public Matrix4x4[][] matricesPerGroup;
            public int[] usedPerGroup;

            // thresholdsPerGroup[g][i] is a stable per-instance value in [0,1]. An instance is affected
            // by vitality loss when its threshold is greater than the group's current vitality. The value
            // never changes after generation, so the same corals bleach or disappear for a given vitality.
            public float[][] thresholdsPerGroup;
        }
    }
}
