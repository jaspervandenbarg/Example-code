using UnityEngine;

namespace MARIS.Rendering.InstanceRendering.Corals
{
    public struct CoralInstance
    {
        public Vector3 position;
        public float rotationY;
        public float scale;

        // index of the healthy coral atlas texture to use for this instance
        // This is used to determine which texture to use when rendering the coral instance
        public ushort healthyAtlasIndex;

        public ushort groupId;

        // if the coral is bleached atles index is healthyAtlasIndex + 1, otherwise it is healthyAtlasIndex
        // so always healthyAtlasIndex + bleached will give the correct atlas index to use for rendering
        // does not always need to be used for example for sea grass or algea
        public byte bleached;
    }
}

