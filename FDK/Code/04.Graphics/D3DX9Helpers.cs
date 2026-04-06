using System;

namespace FDK
{
    /// <summary>
    /// Math utilities originally from D3DX9. Only PerspectiveFovLH remains,
    /// since System.Numerics.Matrix4x4.CreatePerspectiveFieldOfView validates
    /// nearPlaneDistance > 0, which the original code violates with -100f.
    /// </summary>
    public static class D3DX9Helpers
    {
        /// <summary>
        /// Left-handed perspective projection matching the original D3D9 Matrix.PerspectiveFovLH.
        /// Unlike System.Numerics, this does not validate nearPlaneDistance > 0.
        /// </summary>
        public static System.Numerics.Matrix4x4 PerspectiveFovLH(float fov, float aspect, float znear, float zfar)
        {
            float yScale = 1.0f / MathF.Tan(fov * 0.5f);
            float xScale = yScale / aspect;
            float zRange = zfar - znear;
            var result = new System.Numerics.Matrix4x4();
            result.M11 = xScale;
            result.M22 = yScale;
            result.M33 = zfar / zRange;
            result.M34 = 1.0f;
            result.M43 = -znear * zfar / zRange;
            return result;
        }
    }
}
