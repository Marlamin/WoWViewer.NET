using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using System.Collections.Concurrent;
using System.Diagnostics;
using WoWRenderLib.DX11.Loaders;
using WoWRenderLib.DX11.Managers;
using WoWRenderLib.DX11.Structs;

namespace WoWRenderLib.DX11.Cache
{
    public static class WMOCache
    {
        private static readonly Dictionary<uint, WorldModel> Cache = [];

        private static readonly Dictionary<uint, List<uint>> Users = [];

        private static ComPtr<ID3D11Device>? cachedDevice = null;

        private static readonly HashSet<uint> inFlight = [];

        private static readonly ConcurrentQueue<uint> parseQueue = [];
        private static readonly ConcurrentQueue<(uint originalFileDataId, PreppedWMO preppedWMO)> uploadQueue = [];

        private static CancellationTokenSource? workerCancellation;
        private static Task? workerTask;

        public static WorldModel GetOrLoad(ComPtr<ID3D11Device> device, uint fileDataId, CompiledShader shaderProgram, uint parent, bool keepTrack = true)
        {
            cachedDevice ??= device;

            StartWorker();

            if (keepTrack)
            {
                if (Users.TryGetValue(fileDataId, out var users))
                    users.Add(parent);
                else
                    Users.Add(fileDataId, [parent]);
            }

            if (Cache.TryGetValue(fileDataId, out WorldModel value))
                return value;

            WorldModel placeholderWMO;

            try
            {
                var preppedWMO = WMOLoader.ParseWMO(112521);
                placeholderWMO = WMOLoader.LoadWMO(preppedWMO, device, shaderProgram); // missingwmo.wmo
            }
            catch (Exception e)
            {
                Console.WriteLine("!!! Error loading placeholder WMO: " + e.Message);
                placeholderWMO = new WorldModel();
            }

            Cache.Add(fileDataId, placeholderWMO);

            if (inFlight.Contains(fileDataId))
                return placeholderWMO;

            inFlight.Add(fileDataId);
            parseQueue.Enqueue(fileDataId);

            return placeholderWMO;
        }

        private static void StartWorker()
        {
            if (workerTask != null)
                return;

            workerCancellation = new CancellationTokenSource();
            workerTask = Task.Run(() => ParseWorker(workerCancellation.Token), workerCancellation.Token);
        }

        private static async Task ParseWorker(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                uint fileDataId = 0;
                bool hasWork = false;

                if (parseQueue.TryDequeue(out fileDataId))
                    hasWork = true;

                if (!hasWork)
                {
                    await Task.Delay(10, cancellationToken);
                    continue;
                }

                try
                {
                    var preppedWMO = WMOLoader.ParseWMO(fileDataId);
                    uploadQueue.Enqueue((fileDataId, preppedWMO));
                }
                catch (Exception e)
                {
                    Console.WriteLine($"!!! Error parsing WMO {fileDataId}: {e.Message}");

                    // Remove from in-flight set so it's not stuck in limbo
                    inFlight.Remove(fileDataId);
                }
            }
        }

        public static void Upload(CompiledShader shaderProgram)
        {
            if (cachedDevice == null)
                return;

            const int maxUploadsPerFrame = 2;
            int uploaded = 0;

            while (uploaded < maxUploadsPerFrame)
            {
                uint originalFileDataId;
                PreppedWMO preppedWMO;

                if (!uploadQueue.TryDequeue(out var item))
                    break;

                (originalFileDataId, preppedWMO) = item;

                if (!Cache.TryGetValue(originalFileDataId, out var oldWMO))
                {
                    inFlight.Remove(originalFileDataId);
                    continue;
                }

                try
                {
                    var newWMO = WMOLoader.LoadWMO(preppedWMO, cachedDevice.Value, shaderProgram);
                    Cache[originalFileDataId] = newWMO;

                    if (oldWMO.groupBatches != null && oldWMO.groupBatches.Length > 0)
                        WMOLoader.UnloadWMO(oldWMO);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"!!! Error uploading WMO {originalFileDataId}: {e.Message}");
                }

                inFlight.Remove(originalFileDataId);

                uploaded++;
            }
        }

        public static void StopWorker()
        {
            workerCancellation?.Cancel();
            workerCancellation?.Dispose();
            workerCancellation = null;
            workerTask = null;
        }

        public static int GetLoadQueueCount()
        {
            return parseQueue.Count + uploadQueue.Count;
        }

        public static void Release(uint fileDataId, uint parent)
        {
            if (Users.TryGetValue(fileDataId, out var users))
            {
                users.Remove(parent);

                if (users.Count == 0)
                {
                    Users.Remove(fileDataId);
                    if (Cache.TryGetValue(fileDataId, out var wmo))
                    {
                        Cache.Remove(fileDataId);
                        WMOLoader.UnloadWMO(wmo);
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

        public static void ReleaseAll()
        {
            Debug.WriteLine("Releasing " + Cache.Count + " cached WMOs.");

            foreach (var key in Cache.Keys)
                if (Cache.TryGetValue(key, out var wmo))
                    WMOLoader.UnloadWMO(wmo);

            Cache.Clear();
            Users.Clear();
        }
    }
}
