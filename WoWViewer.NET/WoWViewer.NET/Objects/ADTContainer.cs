using Silk.NET.OpenGL;

namespace WoWViewer.NET.Objects
{
    public class ADTContainer : Container3D
    {
        public Renderer.Structs.Terrain Terrain { get; }

        public ADTContainer(GL gl, Renderer.Structs.Terrain terrain, uint fileDataID, uint shaderProgram) : base(gl, fileDataID, shaderProgram)
        {
            Terrain = terrain;
        }
    }
}
