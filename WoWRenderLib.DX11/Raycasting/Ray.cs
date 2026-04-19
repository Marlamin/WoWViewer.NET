using System.Numerics;

namespace WoWRenderLib.DX11.Raycasting
{
    public struct Ray
    {
        public Vector3 Origin { get; set; }
        public Vector3 Direction { get; set; }

        public Ray(Vector3 origin, Vector3 direction)
        {
            Origin = origin;
            Direction = Vector3.Normalize(direction);
        }

        public Vector3 GetPoint(float distance)
        {
            return Origin + Direction * distance;
        }
    }
}
