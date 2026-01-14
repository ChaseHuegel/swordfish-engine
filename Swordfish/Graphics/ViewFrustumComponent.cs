using Swordfish.ECS;
using Swordfish.Library.Util;

namespace Swordfish.Graphics;

public struct ViewFrustumComponent : IDataComponent
{
    public FieldOfView FOV;
    public float NearPlane;
    public float FarPlane;
    
    public ViewFrustumComponent(float nearPlane, float farPlane, int fovDegrees)
    {
        NearPlane = nearPlane;
        FarPlane = farPlane;
        FOV = new FieldOfView(fovDegrees);
    }
    
    public ViewFrustumComponent(float nearPlane, float farPlane, float fovRadians)
    {
        NearPlane = nearPlane;
        FarPlane = farPlane;
        FOV = new FieldOfView(fovRadians);
    }

    public struct FieldOfView
    {
        public int Degrees
        {
            get => _degrees;
            set
            {
                _degrees = value;
                _radians = MathS.DEGREES_TO_RADIANS * value;
            }
        }
        
        public float Radians
        {
            get => _radians;
            set
            {
                _degrees = (int)(value / MathS.DEGREES_TO_RADIANS);
                _radians = value;
            }
        }

        private int _degrees;
        private float _radians;
        
        public FieldOfView(int degrees)
        {
            Degrees = degrees;
        }
        
        public FieldOfView(float radians)
        {
            Radians = radians;
        }
    }
}