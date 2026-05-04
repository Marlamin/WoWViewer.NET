using Silk.NET.OpenGL;
using System.Diagnostics;
using WoWRenderLib.OpenGL.Loaders;
using WoWRenderLib.OpenGL.Structs;

namespace WoWRenderLib.OpenGL.Cache
{
    public static class M2Cache
    {
        private static readonly Dictionary<uint, ParsedDoodadBatch> Cache = [];
        private static readonly Dictionary<uint, List<uint>> Users = [];

        public static ParsedDoodadBatch GetOrLoad(GL gl, uint fileDataId, uint shaderProgram, uint parent)
        {
            if (Users.TryGetValue(fileDataId, out var users))
                users.Add(parent);
            else
                Users.Add(fileDataId, [parent]);

            if (Cache.TryGetValue(fileDataId, out ParsedDoodadBatch value))
                return value;

            try
            {
                var model = WoWRenderLib.Loaders.M2Loader.ParseM2(fileDataId);
                Cache.Add(fileDataId, M2Loader.LoadM2(gl, model, shaderProgram));
            }
            catch (Exception e)
            {
                var model = WoWRenderLib.Loaders.M2Loader.ParseM2(166046);
                Console.WriteLine("Error loading M2 " + fileDataId + ": " + e.Message);
                Cache.Add(fileDataId, M2Loader.LoadM2(gl, model, shaderProgram));
            }

            return Cache[fileDataId];
        }

        public static void Release(GL gl, uint fileDataId, uint parent)
        {
            if (Users.TryGetValue(fileDataId, out var users))
            {
                users.Remove(parent);
                if (users.Count == 0)
                {
                    Users.Remove(fileDataId);
                    if (Cache.TryGetValue(fileDataId, out var model))
                    {
                        gl.DeleteVertexArray(model.vao);
                        gl.DeleteBuffer(model.vertexBuffer);
                        gl.DeleteBuffer(model.indiceBuffer);

                        foreach (var material in model.mats)
                        {
                            BLPCache.Release(gl, material.fileDataID, fileDataId);
                        }

                        Cache.Remove(fileDataId);
                    }
                }
                else
                {
                    Users[fileDataId] = users;
                }
            }
        }

        public static int GetCacheCount()
        {
            return Cache.Count;
        }

        public static void ReleaseAll(GL gl)
        {
            Debug.WriteLine("Releasing " + Cache.Count + " cached M2s.");

            foreach (var item in Users)
            {
                var fileDataId = item.Key;
                var parents = new List<uint>(item.Value);
                foreach (var parent in parents)
                    Release(gl, fileDataId, parent);
            }

            Cache.Clear();
            Users.Clear();
        }
    }
}
