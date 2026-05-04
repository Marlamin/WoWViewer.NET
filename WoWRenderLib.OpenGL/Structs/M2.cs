using WoWRenderLib.Structs;

namespace WoWRenderLib.OpenGL.Structs
{
    public struct ParsedDoodadBatch
    {
        public uint vao;
        public uint vertexBuffer;
        public uint indiceBuffer;
        public uint[] indices;
        public uint fileDataID;
        public BoundingBox boundingBox;
        public float boundingRadius;
        public Submesh[] submeshes;
        public M2Material[] mats;
    }
}
