using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using System.Collections.Concurrent;
using System.Diagnostics;
using WoWRenderLib.DX11.Loaders;
using WoWRenderLib.DX11.Structs;

namespace WoWRenderLib.DX11.Cache
{
    public static class ADTCache
    {
        private static readonly ConcurrentDictionary<string, Terrain> Cache = [];
        private static readonly ConcurrentDictionary<string, List<uint>> Users = [];
        private static readonly ConcurrentDictionary<string, Action<Terrain>> Callbacks = [];

        private static readonly Lock inFlightLock = new();
        private static readonly HashSet<string> inFlight = [];
        private static readonly ConcurrentQueue<(string key, MapTile mapTile)> parseQueue = [];
        private static readonly ConcurrentQueue<(string key, ParsedADT parsedADT)> uploadQueue = [];

        private static CancellationTokenSource? workerCancellation;
        private static Task? workerTask;

        public static Terrain GetOrLoad(MapTile mapTile, uint parent, Action<Terrain>? onLoaded = null, bool keepTrack = true)
        {
            StartWorker();

            var key = (mapTile.wdtFileDataID, mapTile.tileX, mapTile.tileY).ToString();


            if (keepTrack)
            {
                if (Users.TryGetValue(key, out var users))
                    users.Add(parent);
                else
                    Users.TryAdd(key, [parent]);
            }

            lock (inFlightLock)
            {
                if (inFlight.Contains(key))
                    return Cache[key];
            }

            if (Cache.TryGetValue(key, out Terrain value))
            {
                // return immediately if already loaded
                if (value.renderBatches != null)
                    onLoaded?.Invoke(value);

                return value;
            }

            // TODO: LOD ADT? Better placeholder? Do in ADT container?
            Cache.TryAdd(key, new Terrain());

            // onLoaded here is the callback to the ADT container to fire for when its loaded
            if (onLoaded != null)
                Callbacks[key] = onLoaded;

            lock(inFlightLock)
                inFlight.Add(key);
            
            parseQueue.Enqueue((key, mapTile));

            return Cache[key];
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
                if (!parseQueue.TryDequeue(out var item))
                {
                    await Task.Delay(10, cancellationToken);
                    continue;
                }

                var (key, mapTile) = item;

                try
                {
                    var parsed = ADTLoader.ParseADT(mapTile);
                    uploadQueue.Enqueue((key, parsed));
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to parse ADT {key}: {e.Message}");
                    
                    lock(inFlightLock)
                        inFlight.Remove(key);
                    
                    Callbacks.TryRemove(key, out _);
                }
            }
        }

        public static void Upload(Stopwatch queueTimer, ComPtr<ID3D11Device> device)
        {
            while (queueTimer.ElapsedMilliseconds < 10)
            {
                if (!uploadQueue.TryDequeue(out var item))
                    return;

                var (key, parsedADT) = item;

                if (!Cache.TryGetValue(key, out var oldTerrain))
                {
                    lock(inFlightLock)
                        inFlight.Remove(key);
                    
                    Callbacks.TryRemove(key, out _);
                    continue;
                }

                try
                {
                    var newTerrain = ADTLoader.LoadADT(device, parsedADT);
                    Cache[key] = newTerrain;

                    if (Callbacks.Remove(key, out var callback))
                        callback(newTerrain);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to upload ADT {parsedADT.rootADTFileDataID}: {e.Message}");
                    Callbacks.TryRemove(key, out _);
                }

                lock(inFlightLock)
                    inFlight.Remove(key);
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

        public static void Release(MapTile mapTile, uint parent)
        {
            var key = (mapTile.wdtFileDataID, mapTile.tileX, mapTile.tileY).ToString();
            if (Users.TryGetValue(key, out var users))
            {
                users.Remove(parent);
                if (users.Count == 0)
                {
                    Users.TryRemove(key, out _);
                    Callbacks.TryRemove(key, out _);
                    if (Cache.TryRemove(key, out var terrain))
                        ADTLoader.UnloadTerrain(terrain);
                }
            }
        }

        public static int GetCacheCount() => Cache.Count;

        public static void ReleaseAll()
        {
            Debug.WriteLine("Releasing " + Cache.Count + " cached ADTs.");

            StopWorker();

            foreach (var key in Cache.Keys)
                if (Cache.TryGetValue(key, out var terrain))
                    ADTLoader.UnloadTerrain(terrain);

            Callbacks.Clear();
            Users.Clear();
            Cache.Clear();
        }
    }
}