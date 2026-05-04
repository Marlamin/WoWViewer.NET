using System.Numerics;
using WoWRenderLib.Structs;

namespace WoWRenderLib.OpenGL.Structs
{
    public struct Terrain
    {
        public uint rootADTFileDataID;
        public uint vao;
        public uint vertexBuffer;
        public uint indiceBuffer;
        public Vector3 startPos;
        public ADTRenderBatch[] renderBatches;
        public WorldModelBatch[] worldModelBatches;
        public Doodad[] doodads;
        public uint[] blpFileDataIDs;
        public Vector4 heights;
        public Vector4 weights;
        public BoundingBox[] chunkBounds;
    }

    public struct ADTRenderBatch
    {
        public int[] materialID;
        public int[] alphaMaterialID;
        public float[] scales;
        public int[] heightMaterialIDs;
        public float[] heightScales;
        public float[] heightOffsets;
    }
}
