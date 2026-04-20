using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using System.Numerics;
using WoWRenderLib.DX11.Cache;
using WoWRenderLib.DX11.Raycasting;
using WoWRenderLib.DX11.Structs;

namespace WoWRenderLib.DX11.Objects
{
    public class M2Container : Container3D
    {
        private bool[]? _enabledGeosets;

        public WMOContainer? ParentWMO { get; set; } = null;
        public Vector3 LocalPosition { get; set; }
        public Quaternion LocalRotation { get; set; }
        public float LocalScale { get; set; } = 1.0f;

        public bool[] EnabledGeosets
        {
            get
            {
                var m2 = GetM2();

                if (_enabledGeosets == null || _enabledGeosets.Length != m2.submeshes.Length)
                {
                    _enabledGeosets = new bool[m2.submeshes.Length];
                    Array.Fill(_enabledGeosets, true);
                }

                return _enabledGeosets;
            }
        }

        public M2Container(ComPtr<ID3D11Device> device, uint fileDataID, uint parentFileDataId) : base(device, fileDataID, parentFileDataId)
        {
            GetM2(true);
        }

        private static float GetMaxScaleFromMatrix(Matrix4x4 m)
        {
            var scaleX = new Vector3(m.M11, m.M12, m.M13).Length();
            var scaleY = new Vector3(m.M21, m.M22, m.M23).Length();
            var scaleZ = new Vector3(m.M31, m.M32, m.M33).Length();

            return MathF.Max(scaleX, MathF.Max(scaleY, scaleZ));
        }

        // the way to deal with scaling here is definitely a bit of a hack
        public override BoundingSphere? GetBoundingSphere()
        {
            var m2 = GetM2();
            var matrix = GetModelMatrix();

            var localCenter = (m2.boundingBox.Min + m2.boundingBox.Max) / 2f;
            var transformedCenter = Vector3.Transform(localCenter, matrix);

            float scale = GetMaxScaleFromMatrix(matrix);

            return new BoundingSphere(transformedCenter, m2.boundingRadius * scale);
        }

        public override BoundingBox? GetBoundingBox()
        {
            return BoundingBox.Transform(GetLocalBoundingBox(), GetModelMatrix());
        }

        public BoundingBox GetLocalBoundingBox()
        {
            var m2 = GetM2();
            return m2.boundingBox;
        }

        public DoodadBatch GetM2(bool keepTrack = false)
        {
            return M2Cache.GetOrLoad(_device, FileDataId, ParentFileDataId, keepTrack);
        }
    }
}
