using Silk.NET.OpenGL;
using WoWViewer.NET.Loaders;
using static WoWViewer.NET.Renderer.Structs;

namespace WoWViewer.NET.Renderer
{
    public static class Cache
    {
        private static Dictionary<string, Terrain> ADTCache = new();
        private static Dictionary<uint, WorldModel> WMOCache = new();
        private static Dictionary<uint, DoodadBatch> M2Cache = new();

        public static DoodadBatch GetOrLoadM2(GL gl, uint fileDataId, uint shaderProgram)
        {
            if (M2Cache.ContainsKey(fileDataId))
                return M2Cache[fileDataId];

            M2Cache.Add(fileDataId, M2Loader.LoadM2(gl, fileDataId, shaderProgram));

            return M2Cache[fileDataId];
        }

        public static WorldModel GetOrLoadWMO(GL gl, uint fileDataId, uint shaderProgram)
        {
            if (WMOCache.ContainsKey(fileDataId))
                return WMOCache[fileDataId];

            WMOCache.Add(fileDataId, WMOLoader.LoadWMO(gl, fileDataId, shaderProgram));

            return WMOCache[fileDataId];
        }
    }
}
