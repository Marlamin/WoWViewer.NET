using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using System.Numerics;
using WoWRenderLib.DX11.Cache;
using WoWRenderLib.DX11.Managers;
using WoWRenderLib.DX11.Raycasting;
using WoWRenderLib.DX11.Structs;

namespace WoWRenderLib.DX11.Objects
{
    public class M2Container : Container3D
    {
        public bool[] EnabledGeosets { get; }

        private DoodadBatch m2;
        public WMOContainer? ParentWMO { get; set; } = null;
        public Vector3 LocalPosition { get; set; }
        public Quaternion LocalRotation { get; set; }
        public float LocalScale { get; set; } = 1.0f;

        private BoundingSphere? CachedBoundingSphere = null;

        public M2Container(ComPtr<ID3D11Device> device, uint fileDataID, CompiledShader shaderProgram, uint parentFileDataId) : base(device, fileDataID, shaderProgram, parentFileDataId)
        {
            m2 = M2Cache.GetOrLoad(device, fileDataID, shaderProgram, parentFileDataId);
            EnabledGeosets = new bool[m2.submeshes.Length];
            Array.Fill(EnabledGeosets, true);
        }

        public override BoundingSphere? GetBoundingSphere()
        {
            if(CachedBoundingSphere.HasValue)
                return CachedBoundingSphere.Value;

            var localBox = GetLocalBoundingBox();
            var transformedCenter = Vector3.Transform((localBox.Min + localBox.Max) / 2f, GetModelMatrix());

            float realScale = Scale;
            
            if(ParentWMO != null)
                realScale = LocalScale * ParentWMO.Scale;

            CachedBoundingSphere = new BoundingSphere(transformedCenter, m2.boundingRadius * realScale);
            return CachedBoundingSphere.Value;
        }

        public override BoundingBox? GetBoundingBox()
        {
            return BoundingBox.Transform(GetLocalBoundingBox(), GetModelMatrix());
        }

        public BoundingBox GetLocalBoundingBox()
        {
            return m2.boundingBox;
        }
    }
}
