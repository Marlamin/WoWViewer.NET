using System.Numerics;

namespace WoWViewer.NET
{
    public class Structs
    {
        public struct Terrain
        {
            public uint rootADTFileDataID;
            public uint vao;
            public uint vertexBuffer;
            public uint indiceBuffer;
            public Vector3 startPos;
            public RenderBatch[] renderBatches;
            public Doodad[] doodads;
            public WorldModelBatch[] worldModelBatches;
            public uint[] blpFileDataIDs;
            public Vector4 heights;
            public Vector4 weights;
            public ChunkBounds[] chunkBounds;
        }

        public struct ChunkBounds
        {
            public Vector3 Min;
            public Vector3 Max;
        }

        public struct ADTVertex
        {
            public Vector3 Normal;
            public Vector4 Color;
            public Vector2 TexCoord;
            public Vector3 Position;
        }

        public struct M2Vertex
        {
            public Vector3 Normal;
            public Vector2 TexCoord;
            public Vector3 Position;
        }

        public struct WMOVertex
        {
            public Vector3 Normal;
            public Vector2 TexCoord;
            public Vector2 TexCoord2;
            public Vector2 TexCoord3;
            public Vector2 TexCoord4;
            public Vector3 Position;
            public Vector4 Color;
            public Vector4 Color2;
            public Vector4 Color3;
        }

        public struct Material
        {
            // M2/ADT
            public uint textureID;

            // WMO
            public int textureID1;
            public int textureID2;
            public int textureID3;
            public int textureID4;
            public int textureID5;
            public int textureID6;
            public int textureID7;
            public int textureID8;
            public int textureID9;
            internal int texture1;
            internal int texture2;
            internal int texture3;
            internal int texture4;
            internal int texture5;
            internal int texture6;
            internal int texture7;
            internal int texture8;
            internal int texture9;

            // ADT
            public float scale;
            public float heightScale;
            public float heightOffset;
            public uint heightTexture;

            public uint blendMode;
            internal WoWFormatLib.Structs.M2.TextureFlags flags;
        }

        public struct RenderBatch
        {
            public int[] materialID;
            /* WMO ONLY */
            public uint firstFace;
            public uint numFaces;
            public uint groupID;
            public uint blendType;
            public uint shader;
            /* ADT ONLY */
            public int[] alphaMaterialID;
            public float[] scales;
            public int[] heightMaterialIDs;
            public float[] heightScales;
            public float[] heightOffsets;
        }

        public struct Doodad
        {
            public uint fileDataID;
            public Vector3 position;
            public Vector3 rotation;
            public float scale;
            public DoodadBatch m2Model;
        }

        public struct DoodadBatch
        {
            public uint vao;
            public uint vertexBuffer;
            public uint indiceBuffer;
            public uint[] indices;
            public BoundingBox boundingBox;
            public float boundingRadius;
            public Submesh[] submeshes;
            public Material[] mats;
        }

        public struct WorldModelBatch
        {
            public Vector3 position;
            public Vector3 rotation;
            public float scale;
            public uint fileDataID;
            public uint uniqueID;
        }

        public struct WMODoodad
        {
            public string filename;
            public uint filedataid;
            public short flags;
            public Vector3 position;
            public Quaternion rotation;
            public float scale;
            public Vector4 color;
            public uint doodadSet;
        }

        public struct BoundingBox
        {
            public Vector3 min;
            public Vector3 max;
        }

        public struct Submesh
        {
            public uint firstFace;
            public uint numFaces;
            public uint material;
            public uint blendType;
            public uint groupID;
        }

        public struct WorldModel
        {
            public uint RootWMOFileDataID;
            public WorldModelGroupBatches[] groupBatches;
            public Material[] mats;
            public RenderBatch[] wmoRenderBatch;
            public WMODoodad[] doodads;
            public string[] doodadSets;
            public Vector3[] boundingBox;
            public float boundingRadius;
        }

        public struct WorldModelGroupBatches
        {
            public uint vao;
            public uint vertexBuffer;
            public uint indiceBuffer;
            public uint verticeCount;
            public string groupName;
        }

        public struct MapTile
        {
            public uint wdtFileDataID;
            public byte tileX;
            public byte tileY;
        }

        public struct WMOGroup
        {
            public string name;
            public uint verticeOffset;
            public WMOVertex[] vertices;
            public uint[] indices;
            public RenderBatch[] renderBatches;
        }
    }
}