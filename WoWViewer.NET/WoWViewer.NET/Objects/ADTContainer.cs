namespace WoWViewer.NET.Objects
{
    public class ADTContainer : Container3D
    {
        public Renderer.Structs.Terrain Terrain { get; }

        public ADTContainer(Renderer.Structs.Terrain terrain, string fileName) : base(fileName)
        {
            Terrain = terrain;
        }
    }
}
