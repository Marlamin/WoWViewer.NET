using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using System.Numerics;
using System.Runtime.InteropServices;

namespace WoWRenderLib.DX11.Structs
{

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ADTPerObjectCB
    {
        public Matrix4x4 model_matrix;
        public Matrix4x4 projection_matrix;
        public Matrix4x4 rotation_matrix;
        public Vector3 firstPos;
        public float _pad0; // pad to 16 byte boundary
    }

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

    [StructLayout(LayoutKind.Sequential, Pack = 16)]
    public struct LayerData
    {
        public int layerCount;
        public Vector3 lightDirection;
        public Vector4 heightScales0;
        public Vector4 heightScales1;
        public Vector4 heightOffsets0;
        public Vector4 heightOffsets1;
        public Vector4 layerScales0;
        public Vector4 layerScales1;
    }

    public struct Doodad
    {
        public uint fileDataID;
        public Vector3 position;
        public Vector3 rotation;
        public float scale;
        public DoodadBatch m2Model;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ADTVertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 TexCoord;
        public Vector4 Color;
    }

    public struct ADTMaterial
    {
        public int texture;
        public int heightTexture;
        public float scale;
        public float heightScale;
        public float heightOffset;
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
