using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using System.Numerics;
using WoWRenderLib.DX11.Managers;
using WoWRenderLib.DX11.Structs;

namespace WoWRenderLib.DX11.Objects
{
    public class ADTContainer : Container3D
    {
        public Terrain Terrain { get; }
        public MapTile mapTile;

        public ADTContainer(ComPtr<ID3D11Device> device, Terrain terrain, MapTile mapTile) : base(device, mapTile.wdtFileDataID, mapTile.wdtFileDataID)
        {
            Terrain = terrain;
            this.mapTile = mapTile;
        }

        public override Matrix4x4 GetModelMatrix()
        {
            if (ModelMatrix.HasValue)
                return ModelMatrix.Value;

            ModelMatrix = Matrix4x4.CreateRotationZ(MathF.PI) * Matrix4x4.CreateScale(-1f, -1f, 1f);

            return ModelMatrix.Value;
        }
    }
}
