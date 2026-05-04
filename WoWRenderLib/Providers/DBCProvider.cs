using CASCLib;
using DBCD.Providers;
using WoWRenderLib.Services;

namespace WoWRenderLib.Providers
{
    public class DBCProvider : IDBCProvider
    {
        public LocaleFlags localeFlags = LocaleFlags.All_WoW;

        public Stream StreamForTableName(string tableName, string build)
        {
            // TODO: Listfile/DBD manifest for name -> ID mappings
            uint fileDataID = 0;
            switch (tableName.ToLower())
            {
                case "map":
                    fileDataID = 1349477;
                    break;
            }

            if (fileDataID == 0)
                throw new Exception("Unmapped DBC " + tableName);
            //end todo

            var db2Stream = new MemoryStream();
            if (CASC.FileExists(fileDataID))
            {
                try
                {
                    var bytes = CASC.buildInstance.OpenFileByFDID(fileDataID);
                    db2Stream.Write(bytes, 0, bytes.Length);
                    db2Stream.Position = 0;
                    return db2Stream;
                }
                catch (Exception e)
                {
                    Console.WriteLine("Unable to extract DB2 from CASC: " + e.Message);
                }
            }

            throw new FileNotFoundException($"Unable to find {tableName} for {build}");
        }
    }
}
