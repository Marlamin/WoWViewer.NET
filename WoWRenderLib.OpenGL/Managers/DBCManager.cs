using CASCLib;
using DBCD;
using DBCD.Providers;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
using WoWRenderLib.Providers;

namespace WoWRenderLib.Managers
{
    public class DBCManager(IDBDProvider dbdProvider, IDBCProvider dbcProvider)
    {
        private readonly DBDProvider dbdProvider = (DBDProvider)dbdProvider;
        private readonly DBCProvider dbcProvider = (DBCProvider)dbcProvider;

        private MemoryCache Cache = new(new MemoryCacheOptions() { SizeLimit = 250 });
        private readonly ConcurrentDictionary<(string, string, bool, LocaleFlags), SemaphoreSlim> Locks = [];

        public async Task<IDBCDStorage> GetOrLoad(string name, string build)
        {
            return await GetOrLoad(name, build, false);
        }

        public async Task<IDBCDStorage> GetOrLoad(string name, string build, bool useHotfixes = false, LocaleFlags locale = LocaleFlags.All_WoW, List<int>? pushIDFilter = null)
        {
            if (locale != LocaleFlags.All_WoW)
            {
                return LoadDBC(name, build, useHotfixes, locale);
            }

            if (pushIDFilter != null)
            {
                return LoadDBC(name, build, useHotfixes, locale, pushIDFilter);
            }

            if (Cache.TryGetValue((name, build, useHotfixes, locale), out var cachedDBC))
                return (DBCD.IDBCDStorage)cachedDBC!;

            SemaphoreSlim mylock = Locks.GetOrAdd((name, build, useHotfixes, locale), k => new SemaphoreSlim(1, 1));

            await mylock.WaitAsync();

            try
            {
                if (!Cache.TryGetValue((name, build, useHotfixes, locale), out cachedDBC))
                {
                    // Key not in cache, load DBC
                    Console.WriteLine("DBC " + name + " for build " + build + " (hotfixes: " + useHotfixes + ") is not cached, loading!");
                    cachedDBC = LoadDBC(name, build, useHotfixes, locale);
                    Cache.Set((name, build, useHotfixes, locale), cachedDBC, new MemoryCacheEntryOptions().SetSize(1));
                }
            }
            finally
            {
                mylock.Release();
            }

            return (DBCD.IDBCDStorage)cachedDBC!;
        }

        private IDBCDStorage LoadDBC(string name, string build, bool useHotfixes = false, LocaleFlags locale = LocaleFlags.All_WoW, List<int>? pushIDFilter = null)
        {
            if (locale != LocaleFlags.All_WoW)
            {
                dbcProvider.localeFlags = locale;
            }

            DBCD.DBCD dbcd;

            // we don't feed enumProvider to DBCD for now
            if (dbdProvider.isUsingBDBD)
                dbcd = new DBCD.DBCD(dbcProvider, DBDProvider.GetBDBDStream());
            else
                dbcd = new DBCD.DBCD(dbcProvider, dbdProvider);

            var storage = dbcd.Load(name, build);

            dbcProvider.localeFlags = locale;

            var splitBuild = build.Split('.');

            if (splitBuild.Length != 4)
            {
                throw new Exception("Invalid build!");
            }

            var buildNumber = uint.Parse(splitBuild[3]);

            if (!useHotfixes)
                return storage;

            /* if (HotfixManager.hotfixReaders.Count == 0)
                 HotfixManager.LoadCaches();

             if (HotfixManager.hotfixReaders.TryGetValue(buildNumber, out HotfixReader? hotfixReaders))
             {
                 // DBCD PR #17 support
                 if (pushIDFilter != null)
                 {
                     storage.ApplyingHotfixes(hotfixReaders, (row, shouldDelete) =>
                     {
                         if (!pushIDFilter.Contains(row.PushId))
                             return RowOp.Ignore;

                         return HotfixReader.DefaultProcessor(row, shouldDelete);
                     });
                 }
                 else
                 {
                     storage.ApplyingHotfixes(hotfixReaders);
                 }
             }
            */
            return storage;
        }

        public void ClearCache()
        {
            Cache.Dispose();
            Cache = new MemoryCache(new MemoryCacheOptions() { SizeLimit = 250 });
        }
    }
}
