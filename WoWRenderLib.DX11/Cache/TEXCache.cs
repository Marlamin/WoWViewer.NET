using WoWFormatLib.FileReaders;
using WoWFormatLib.Structs.TEX;

namespace WoWRenderLib.DX11.Cache
{
    public static class TEXCache
    {
        public static TEXFile? cachedTEX = null;

        public static void Preload(uint fileDataID)
        {
            var texReader = new TEXReader();
            cachedTEX = texReader.LoadTEX(fileDataID);
        }

        public static void ReleaseAll()
        {
            cachedTEX = null;
        }
    }
}
