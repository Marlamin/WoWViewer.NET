using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using System.Collections.Concurrent;
using System.Diagnostics;
using WoWRenderLib.DX11.Loaders;
using WoWRenderLib.DX11.Structs;

namespace WoWRenderLib.DX11.Cache
{
    public static class M2Cache
    {
        private static readonly Dictionary<uint, DoodadBatch> Cache = [];
        private static readonly Dictionary<uint, List<uint>> Users = [];

        private static ComPtr<ID3D11Device>? cachedDevice = null;

        private static readonly HashSet<uint> inFlight = [];
        private static readonly ConcurrentQueue<uint> parseQueue = [];
        private static readonly ConcurrentQueue<(uint originalFileDataId, ParsedM2 parsedM2)> uploadQueue = [];

        private static CancellationTokenSource? workerCancellation;
        private static Task? workerTask;

        public static DoodadBatch GetOrLoad(ComPtr<ID3D11Device> device, uint fileDataId, uint parent, bool keepTrack = true)
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

            if (Cache.TryGetValue(fileDataId, out DoodadBatch value))
                return value;

            DoodadBatch placeholder;
            try
            {
                placeholder = M2Loader.LoadM2(device, M2Loader.ParseM2(166046));
            }
            catch (Exception e)
            {
                Console.WriteLine("!!! Error loading placeholder M2: " + e.Message);
                placeholder = new DoodadBatch();
            }

            Cache.Add(fileDataId, placeholder);

            if (inFlight.Contains(fileDataId))
                return placeholder;

            inFlight.Add(fileDataId);
            parseQueue.Enqueue(fileDataId);

            return placeholder;
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
                if (!parseQueue.TryDequeue(out var fileDataId))
                {
                    await Task.Delay(10, cancellationToken);
                    continue;
                }

                try
                {
                    var parsed = M2Loader.ParseM2(fileDataId);
                    uploadQueue.Enqueue((fileDataId, parsed));
                }
                catch (Exception e)
                {
                    Console.WriteLine($"!!! Error parsing M2 {fileDataId}: {e.Message}");
                    inFlight.Remove(fileDataId);
                }
            }
        }

        public static void Upload(Stopwatch queueTimer)
        {
            if (cachedDevice == null)
                return;

            while (queueTimer.ElapsedMilliseconds < 10)
            {
                if (!uploadQueue.TryDequeue(out var item))
                    return;

                var (originalFileDataId, parsedM2) = item;

                if (!Cache.TryGetValue(originalFileDataId, out var oldBatch))
                {
                    inFlight.Remove(originalFileDataId);
                    return;
                }

                try
                {
                    var newBatch = M2Loader.LoadM2(cachedDevice.Value, parsedM2);
                    Cache[originalFileDataId] = newBatch;

                    unsafe
                    {
                        if (oldBatch.vertexBuffer.Handle != null)
                            M2Loader.UnloadM2(oldBatch);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"!!! Error uploading M2 {originalFileDataId}: {e.Message}");
                }

                inFlight.Remove(originalFileDataId);
            }
        }

        public static void StopWorker()
        {
            workerCancellation?.Cancel();
            workerCancellation?.Dispose();
            workerCancellation = null;
            workerTask = null;
        }

        public static int GetLoadQueueCount() => parseQueue.Count + uploadQueue.Count;

        public static void Release(uint fileDataId, uint parent)
        {
            if (Users.TryGetValue(fileDataId, out var users))
            {
                users.Remove(parent);

                if (users.Count == 0)
                {
                    Users.Remove(fileDataId);
                    if (Cache.TryGetValue(fileDataId, out var model))
                    {
                        Cache.Remove(fileDataId);
                        M2Loader.UnloadM2(model);
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

        public static void CheckUsers()
        {
            var m2sToRemove = new List<uint>();
            foreach (var cachedM2 in Cache.Keys)
                if (!Users.ContainsKey(cachedM2))
                    m2sToRemove.Add(cachedM2);

            foreach (var m2Id in m2sToRemove)
            {
                if (Cache.TryGetValue(m2Id, out var m2))
                {
                    Cache.Remove(m2Id);
                    M2Loader.UnloadM2(m2);
                }
            }
        }

        public static void ReleaseAll()
        {
            Debug.WriteLine("Releasing " + Cache.Count + " cached M2s.");

            foreach (var key in Cache.Keys)
                if (Cache.TryGetValue(key, out var model))
                    M2Loader.UnloadM2(model);

            Cache.Clear();
            Users.Clear();
        }
    }
}