using Silk.NET.OpenGL;
using WoWViewer.NET.Renderer;

namespace WoWViewer.NET.Objects
{
    public class WMOContainer : Container3D
    {
        public bool[] EnabledGroups { get; }
        public bool[] EnabledDoodadSets { get; }

        public WMOContainer(GL gl, uint fileDataID, uint shaderProgram) : base(gl, fileDataID, shaderProgram)
        {
            var wmo = Cache.GetOrLoadWMO(gl, fileDataID, shaderProgram);
            EnabledGroups = new bool[wmo.groupBatches.Length];
            EnabledDoodadSets = new bool[wmo.doodadSets.Length];

            // Is there no way to initialize an array of true bools?
            for (int i = 0; i < EnabledGroups.Length; i++)
                EnabledGroups[i] = true;

            for (int i = 0; i < EnabledDoodadSets.Length; i++)
                EnabledDoodadSets[i] = true;
        }
    }
}
