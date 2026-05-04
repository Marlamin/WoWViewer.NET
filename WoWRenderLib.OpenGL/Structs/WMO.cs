using WoWRenderLib.Structs;

namespace WoWRenderLib.OpenGL.Structs
{
    public struct WMOMaterial
    {
        public int textureID1;
        public int textureID2;
        public int textureID3;
        public int textureID4;
        public int textureID5;
        public int textureID6;
        public int textureID7;
        public int textureID8;
        public int textureID9;
    }

    public struct WorldModel
    {
        public uint rootWMOFileDataID;
        public WorldModelGroupBatches[] groupBatches;
        public WMOMaterial[] mats;
        public PreppedWMOMaterial[] preppedMats;
        public WMORenderBatch[] wmoRenderBatch;
        public WMODoodad[] doodads;
        public string[] doodadSets;
        public BoundingBox boundingBox;
        public float boundingRadius;
    }

    public readonly struct WorldModelGroupBatches
    {
        public readonly uint vao { get; init; }
        public readonly uint vertexBuffer { get; init; }
        public readonly uint indiceBuffer { get; init; }
        public readonly uint verticeCount { get; init; }
        public readonly string groupName { get; init; }
    }

    public struct WMOGroup
    {
        public string name;
        public uint verticeOffset;
        public WMOVertex[] vertices;
        public uint[] indices;
        public WMORenderBatch[] renderBatches;
    }
}
