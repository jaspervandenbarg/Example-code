using System.Collections.Generic;
using MARIS.ScenarioSimulation.SepeModel;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MARIS.Rendering.InstanceRendering.Corals
{
    /// <summary>Which channel of a density mask drives a coral group's density.</summary>
    public enum DensityChannel
    {
        Red = 0,
        Green = 1,
        Blue = 2,
        Alpha = 3,
    }

    /// <summary>How a coral group reacts when its species vitality drops below full.</summary>
    public enum VitalityResponse
    {
        /// <summary>Affected corals vanish (rendered at scale 0). The rest stay as originals.</summary>
        Disappear = 0,
        /// <summary>Affected corals swap to the bleached atlas texture at <c>atlasIndex + 1</c>.</summary>
        Bleach = 1,
    }

    /// <summary>
    /// Describes a single group of coral instances. Every instance in a group shares
    /// the same mesh, atlas texture index and material properties; only the per-instance
    /// transform (position, rotation and scale) differs. A whole group is rendered with a
    /// single <see cref="MaterialPropertyBlock"/>, so it can be drawn as a handful of
    /// instanced batches that all reuse one shared material.
    /// </summary>
    [System.Serializable]
    public class CoralRenderGroup
    {
        [FoldoutGroup("$name"), LabelText("Name")]
        [Tooltip("Display name. Only used to keep the inspector readable.")]
        public string name = "Coral Group";

        [FoldoutGroup("$name")]
        [Tooltip("Mesh used by every coral in this group. When empty, the manager's shared mesh is used.")]
        public Mesh mesh;

        [FoldoutGroup("$name")]
        [Tooltip("Maximum coral instances considered in EACH world cell for this group. The density mask " +
                 "and density mode decide how many of these are actually placed. Total rendered instances " +
                 "grow with the number of visible cells, so keep this modest.")]
        [Min(0)] public int instancesPerCell = 64;

        [FoldoutGroup("$name"), Header("Density Mask")]
        [Tooltip("Which density mask drives this group: -1 = none (always full density), 0 = mask A, 1 = mask B.")]
        [Range(-1, 1)] public int densityMaskIndex = -1;

        [FoldoutGroup("$name")]
        [Tooltip("Which channel of the chosen mask controls this group's density.")]
        public DensityChannel densityChannel = DensityChannel.Red;

        [FoldoutGroup("$name")]
        [Tooltip("Density values below this threshold place no coral (lets you carve clean edges).")]
        [Range(0f, 1f)] public float densityCutoff = 0.05f;

        [FoldoutGroup("$name"), Header("Slope Alignment")]
        [Tooltip("How much each coral tilts to match the terrain slope. 0 = always upright, 1 = fully follows the surface normal.")]
        [Range(0f, 1f)] public float slopeInfluence = 0.5f;

        [FoldoutGroup("$name")]
        [Tooltip("Random +/- variation added to slopeInfluence per instance, so corals on the same slope don't all lean identically.")]
        [Range(0f, 1f)] public float slopeRandomness = 0.15f;

        [FoldoutGroup("$name")]
        [Tooltip("Offset to the normal of the surface, so corals don't all sit flush and some appear to float.")]
        [Range(0f, 1f)] public float normalOffset = 0f;
        
        [FoldoutGroup("$name")] 
        [Tooltip("Random offset to the normal for more variation.")]
        [Range (0f, 1f)] public float normalOffsetRandomness = 0.1f;

        [FoldoutGroup("$name")]
        [Tooltip("Index into the Texture2DArray of the shared material (_MainTexIndex) for this group.")]
        [Min(0)] public int atlasIndex = 0;

        [FoldoutGroup("$name")]
        [Tooltip("Smallest uniform scale a coral in this group can be spawned with.")]
        [Min(0.001f)] public float minScale = 0.5f;

        [FoldoutGroup("$name")]
        [Tooltip("Largest uniform scale a coral in this group can be spawned with.")]
        [Min(0.001f)] public float maxScale = 1.5f;

        [FoldoutGroup("$name")]
        [Header("Surface (_Smoothness / _Metallic)")]
        [Range(0f, 1f)] public float smoothness = 0.5f;
        [FoldoutGroup("$name")]
        [Range(0f, 1f)] public float metallic = 0f;

        [FoldoutGroup("$name")]
        [Header("Wave / Bend")]
        [Tooltip("How strongly the coral reacts to wave movement (_WaveIntensity).")]
        public float waveIntensity = 1f;

        [FoldoutGroup("$name")]
        [Tooltip("Constant bend applied to the coral (_BendOffset).")]
        public float bendOffset = 0f;

        [FoldoutGroup("$name")]
        [Tooltip("How much the coral bends vertically (_VerticalBendStrength).")]
        public float verticalBendStrength = 1f;

        [FoldoutGroup("$name")]
        [Header("Color (_ColorMultiplier)")]
        public Color colorMultiplier = Color.white;

        [FoldoutGroup("$name"), Header("Vitality")]
        [Tooltip("Species key that drives this group. Must match a key in the SEPE species catalogue.")]
        [ValueDropdown(nameof(GetSpeciesKeys))]
        public string speciesKey;

        [FoldoutGroup("$name")]
        [Tooltip("How corals react when vitality drops: Disappear = affected corals vanish, " +
                 "Bleach = affected corals use the atlas texture at atlasIndex + 1.")]
        public VitalityResponse vitalityResponse = VitalityResponse.Disappear;

        /// <summary>Species keys from the SEPE catalogue, used to populate the inspector dropdown.</summary>
        private static IEnumerable<string> GetSpeciesKeys() => SpeciesCatalogue.Species.Keys;
    }
}
