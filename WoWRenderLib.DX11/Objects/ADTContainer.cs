using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using System.Numerics;
using WoWRenderLib.DX11.Cache;
using WoWRenderLib.DX11.Structs;

namespace WoWRenderLib.DX11.Objects
{
    public class ADTContainer : Container3D
    {
        public Terrain Terrain { get; private set; }
        public MapTile mapTile;
        public event Action<ADTContainer, Terrain>? LoadCallback;
        public bool IsLoaded { get; private set; }

        public ADTContainer(ComPtr<ID3D11Device> device, MapTile mapTile) : base(device, mapTile.wdtFileDataID, mapTile.wdtFileDataID)
        {
            // TODO: LOD ADTs or premade placeholder Terrain?
            this.mapTile = mapTile;
        }

        public void UpdateTerrain(Terrain terrain)
        {
            Terrain = terrain;
        }

        public void OnLoaded(Terrain terrain)
        {
            // this gets called by the cache when it finishes (up)loading terrain
            UpdateTerrain(terrain);
            IsLoaded = true;
            LoadCallback?.Invoke(this, terrain); // and in turn we left scene manager know it loaded!
        }

        public void Unload()
        {
            if (IsLoaded)
                ADTCache.Release(mapTile, mapTile.wdtFileDataID);

            IsLoaded = false;
            Terrain = default;
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
