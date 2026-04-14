using Silk.NET.OpenGL;

namespace WoWRenderLib.Structs
{
    public struct DecodedBLP
    {
        public uint FileDataId;
        public byte[] PixelData;
        public int Width;
        public int Height;
        public bool IsCompressed;
        public InternalFormat CompressedFormat;
        public List<MipLevel>? MipLevels;
    }

    public struct MipLevel
    {
        public byte[] Data;
        public int Width;
        public int Height;
        public int Level;
    }
}
