/*
* Copyright (c) 2007-2009 SlimDX Group
* 
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the "Software"), to deal
* in the Software without restriction, including without limitation the rights
* to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
* copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions:
* 
* The above copyright notice and this permission notice shall be included in
* all copies or substantial portions of the Software.
* 
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
* THE SOFTWARE.
*/
using System;
using Vortice.Direct3D11;
using Vortice.DXGI;
using FDK;

namespace SampleFramework
{
    /// <summary>
    /// Manages the D3D11 device, context, swap chain, and sprite renderer.
    /// </summary>
    public class CGraphicsDevice : IDisposable
    {
        GraphicsDeviceManager manager;

        /// <summary>
        /// The D3D11 device (used for resource creation).
        /// </summary>
        public Device Device { get; internal set; }

        /// <summary>
        /// The immediate device context (used for draw commands).
        /// </summary>
        public ID3D11DeviceContext Context { get; internal set; }

        /// <summary>
        /// The DXGI swap chain.
        /// </summary>
        public IDXGISwapChain1 SwapChain { get; internal set; }

        /// <summary>
        /// Render target view for the back buffer.
        /// </summary>
        public ID3D11RenderTargetView RenderTargetView { get; internal set; }

        /// <summary>
        /// The sprite batch renderer.
        /// </summary>
        public SpriteBatch SpriteBatch { get; internal set; }

        internal CGraphicsDevice(GraphicsDeviceManager manager)
        {
            this.manager = manager;
        }

        /// <summary>
        /// Creates the render target view from the swap chain's back buffer.
        /// Call after device/swap chain creation and after each resize.
        /// </summary>
        internal void CreateRenderTargetView()
        {
            RenderTargetView?.Dispose();
            using var backBuffer = SwapChain.GetBuffer<ID3D11Texture2D>(0);
            RenderTargetView = Device.CreateRenderTargetView(backBuffer);
            Context.OMSetRenderTargets(RenderTargetView);
        }

        /// <summary>
        /// Sets the viewport to match the given dimensions.
        /// </summary>
        internal void SetViewport(int width, int height)
        {
            Context.RSSetViewport(0, 0, width, height);
        }

        /// <summary>
        /// Creates a render target texture.
        /// </summary>
        public ShaderResourceTexture CreateRenderTarget(int width, int height)
        {
            var desc = new Texture2DDescription
            {
                Width = (uint)width,
                Height = (uint)height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
            };
            var tex = Device.CreateTexture2D(desc);
            var srv = Device.CreateShaderResourceView(tex);
            return new ShaderResourceTexture { Texture2D = tex, SRV = srv };
        }

        /// <summary>
        /// Copies the back buffer into a texture (for screenshots or render-to-texture).
        /// </summary>
        public void ResolveBackBuffer(ShaderResourceTexture target, int backBufferIndex = 0)
        {
            using var backBuffer = SwapChain.GetBuffer<ID3D11Texture2D>((uint)backBufferIndex);
            if (target.Texture2D != null)
            {
                Context.CopyResource(target.Texture2D, backBuffer);
            }
        }

        /// <summary>
        /// Returns the back buffer texture directly (caller must dispose).
        /// </summary>
        public ID3D11Texture2D ResolveBackBuffer()
        {
            return SwapChain.GetBuffer<ID3D11Texture2D>(0);
        }

        /// <summary>
        /// Resets the render target to the default back buffer.
        /// </summary>
        public void ResetRenderTarget()
        {
            Context.OMSetRenderTargets(RenderTargetView);
        }

        public void Dispose()
        {
            SpriteBatch?.Dispose();
            RenderTargetView?.Dispose();
            SwapChain?.Dispose();
            Context?.Dispose();
            Device?.Dispose();
        }
    }
}
