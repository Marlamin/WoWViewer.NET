using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using System.Numerics;
using WoWRenderLib.Structs;

namespace WoWRenderLib.DX11.Structs
{
    public struct Terrain
    {
        public uint rootADTFileDataID;
        public uint vao;
        public ComPtr<ID3D11Buffer> vertexBuffer;
        public ComPtr<ID3D11Buffer> indiceBuffer;
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
        public int[] materialFDIDs;
        public int[] heightMaterialFDIDs;
        public ComPtr<ID3D11ShaderResourceView>[] alphaMaterialID;
        public float[] scales;
        public float[] heightScales;
        public float[] heightOffsets;
    }
}
