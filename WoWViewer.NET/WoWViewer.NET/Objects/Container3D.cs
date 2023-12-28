using System.Numerics;

namespace WoWViewer.NET.Objects
{
    public class Container3D
    {
        public string FileName { get; }
        public Vector3 Position { get; set; }

        public Container3D(string fileName)
        {
            FileName = fileName;
        }
    }
}
