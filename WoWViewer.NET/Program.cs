using Silk.NET.Windowing;
using WoWRenderLib;

namespace WoWViewer.NET
{

    // Silk window app
    internal class Program
    {
        static void Main(string[] args)
        {
            string wowDir = "";
            string wowProduct = "";

            string buildConfig = "";
            string cdnConfig = "";

            if (args.Length > 0)
                wowDir = args[0];

            if (args.Length > 1)
                wowProduct = args[1];

            if (args.Length > 2)
                buildConfig = args[2];

            if (args.Length > 3)
                cdnConfig = args[3];

            WowClientConfig wowConfig = new WowClientConfig
            {
                wowDir = wowDir,
                wowProduct = wowProduct,
                buildConfig = buildConfig,
                cdnConfig = cdnConfig
            };
            var host = new SilkWindowHost(wowConfig);
            host.Run();

        }
    }
}
