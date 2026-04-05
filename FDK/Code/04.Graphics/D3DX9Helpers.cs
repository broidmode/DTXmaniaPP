using System;
using System.IO;
using System.Runtime.InteropServices;
using Vortice.Direct3D9;

namespace FDK
{
    /// <summary>
    /// P/Invoke wrappers for D3DX9 functions that are not exposed by Vortice.
    /// Requires d3dx9_43.dll to be present in the application directory.
    /// </summary>
    public static class D3DX9Helpers
    {
        // D3DX Filter flags (from d3dx9tex.h)
        public const int D3DX_FILTER_NONE = 0x00000001;
        public const int D3DX_FILTER_POINT = 0x00000002;
        public const int D3DX_FILTER_LINEAR = 0x00000003;
        public const int D3DX_DEFAULT = unchecked((int)0xFFFFFFFF);

        [DllImport("d3dx9_43.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int D3DXCreateTextureFromFileInMemoryEx(
            IntPtr pDevice,
            IntPtr pSrcData,
            uint srcDataSize,
            uint width,
            uint height,
            uint mipLevels,
            int usage,
            int format,
            int pool,
            int filter,
            int mipFilter,
            int colorKey,
            IntPtr pSrcInfo,
            IntPtr pPalette,
            out IntPtr ppTexture);

        [DllImport("d3dx9_43.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int D3DXGetImageInfoFromFileInMemory(
            IntPtr pSrcData,
            uint srcDataSize,
            out D3DXImageInfo pSrcInfo);

        [DllImport("d3dx9_43.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private static extern int D3DXSaveSurfaceToFileW(
            string pDestFile,
            int destFormat,
            IntPtr pSrcSurface,
            IntPtr pSrcPalette,
            IntPtr pSrcRect);

        // D3DXIMAGE_FILEFORMAT values
        public const int D3DXIFF_BMP = 0;
        public const int D3DXIFF_JPG = 1;
        public const int D3DXIFF_PNG = 3;

        [StructLayout(LayoutKind.Sequential)]
        public struct D3DXImageInfo
        {
            public uint Width;
            public uint Height;
            public uint Depth;
            public uint MipLevels;
            public int Format;       // D3DFORMAT
            public int ResourceType; // D3DRESOURCETYPE
            public int ImageFileFormat;
        }

        /// <summary>
        /// Creates a texture from image data in memory using D3DX (supports BMP, JPG, PNG, TGA, DDS, etc.)
        /// Equivalent to SharpDX's Texture.FromMemory / Texture.FromStream.
        /// </summary>
        public static unsafe IDirect3DTexture9 CreateTextureFromMemory(
            IDirect3DDevice9 device,
            byte[] data,
            int width,
            int height,
            int mipLevels,
            Usage usage,
            Format format,
            Pool pool,
            int filter,
            int mipFilter,
            int colorKey)
        {
            fixed (byte* pData = data)
            {
                int hr = D3DXCreateTextureFromFileInMemoryEx(
                    device.NativePointer,
                    (IntPtr)pData,
                    (uint)data.Length,
                    (uint)width,
                    (uint)height,
                    (uint)mipLevels,
                    (int)usage,
                    (int)format,
                    (int)pool,
                    filter,
                    mipFilter,
                    colorKey,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    out IntPtr ppTexture);

                if (hr < 0)
                    Marshal.ThrowExceptionForHR(hr);

                return new IDirect3DTexture9(ppTexture);
            }
        }

        /// <summary>
        /// Creates a texture from a Stream using D3DX.
        /// Equivalent to SharpDX's Texture.FromStream.
        /// </summary>
        public static IDirect3DTexture9 CreateTextureFromStream(
            IDirect3DDevice9 device,
            Stream stream,
            int width,
            int height,
            int mipLevels,
            Usage usage,
            Format format,
            Pool pool,
            int filter,
            int mipFilter,
            int colorKey)
        {
            byte[] data;
            if (stream is MemoryStream ms)
            {
                data = ms.ToArray();
            }
            else
            {
                using var ms2 = new MemoryStream();
                stream.CopyTo(ms2);
                data = ms2.ToArray();
            }
            return CreateTextureFromMemory(device, data, width, height, mipLevels, usage, format, pool, filter, mipFilter, colorKey);
        }

        /// <summary>
        /// Gets image information from image data in memory.
        /// Equivalent to SharpDX's ImageInformation.FromMemory.
        /// </summary>
        public static unsafe D3DXImageInfo GetImageInfoFromMemory(byte[] data)
        {
            fixed (byte* pData = data)
            {
                int hr = D3DXGetImageInfoFromFileInMemory(
                    (IntPtr)pData,
                    (uint)data.Length,
                    out D3DXImageInfo info);

                if (hr < 0)
                    Marshal.ThrowExceptionForHR(hr);

                return info;
            }
        }

        /// <summary>
        /// Saves a surface to a file using D3DX.
        /// Equivalent to SharpDX's Surface.ToFile.
        /// </summary>
        public static void SaveSurfaceToFile(IDirect3DSurface9 surface, string destFile, int imageFileFormat)
        {
            int hr = D3DXSaveSurfaceToFileW(destFile, imageFileFormat, surface.NativePointer, IntPtr.Zero, IntPtr.Zero);
            if (hr < 0)
                Marshal.ThrowExceptionForHR(hr);
        }
    }

    /// <summary>
    /// Extension methods for IDirect3DDevice9 to provide convenient wrappers
    /// for methods whose signatures changed between SharpDX and Vortice.
    /// </summary>
    public static class D3D9Extensions
    {
        // D3DTS_WORLD = D3DTS_WORLDMATRIX(0) = 256
        public const TransformState TransformState_World = (TransformState)256;

        /// <summary>
        /// Left-handed perspective projection matching SharpDX's Matrix.PerspectiveFovLH.
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

        /// <summary>
        /// Draws non-indexed user primitives from an array of vertices.
        /// Replaces SharpDX's Device.DrawUserPrimitives&lt;T&gt;().
        /// </summary>
        public static unsafe void DrawUserPrimitives<T>(
            this IDirect3DDevice9 device,
            PrimitiveType primitiveType,
            uint primitiveCount,
            T[] vertexData) where T : unmanaged
        {
            fixed (T* pData = vertexData)
            {
                device.DrawPrimitiveUP(primitiveType, primitiveCount, (IntPtr)pData, (uint)sizeof(T));
            }
        }

        /// <summary>
        /// Overload matching SharpDX's 4-arg pattern (with startIndex).
        /// The startIndex is ignored (always 0 in this codebase).
        /// </summary>
        public static void DrawUserPrimitives<T>(
            this IDirect3DDevice9 device,
            PrimitiveType primitiveType,
            int startIndex,
            int primitiveCount,
            T[] vertexData) where T : unmanaged
        {
            DrawUserPrimitives(device, primitiveType, (uint)primitiveCount, vertexData);
        }

        /// <summary>
        /// Gets device capabilities. Wraps the out-parameter pattern.
        /// Replaces SharpDX's Device.Capabilities property.
        /// </summary>
        public static Capabilities GetDeviceCaps(this IDirect3DDevice9 device)
        {
            var caps = default(Capabilities);
            device.GetDeviceCaps(ref caps);
            return caps;
        }

        /// <summary>
        /// StretchRect overload that uses full source and destination surfaces.
        /// Equivalent to passing null for source and dest rects in native D3D9.
        /// </summary>
        public static void StretchRectFull(
            this IDirect3DDevice9 device,
            IDirect3DSurface9 source,
            IDirect3DSurface9 dest,
            TextureFilter filter)
        {
            var srcDesc = source.Description;
            var dstDesc = dest.Description;
            var srcRect = new Rect(0, 0, (int)srcDesc.Width, (int)srcDesc.Height);
            var dstRect = new Rect(0, 0, (int)dstDesc.Width, (int)dstDesc.Height);
            device.StretchRect(source, srcRect, dest, dstRect, filter);
        }
    }
}
