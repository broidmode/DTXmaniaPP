using System;
using Vortice.Direct3D11;

namespace FDK;

/// <summary>
/// Wraps an ID3D11Texture2D and its associated ShaderResourceView.
/// Used as the backing type for the global "Texture" alias.
/// </summary>
public class ShaderResourceTexture : IDisposable
{
	public ID3D11Texture2D? Texture2D { get; set; }
	public ID3D11ShaderResourceView? SRV { get; set; }

	public bool IsDisposed { get; private set; }

	public void Dispose()
	{
		if (!IsDisposed)
		{
			SRV?.Dispose();
			SRV = null;
			Texture2D?.Dispose();
			Texture2D = null;
			IsDisposed = true;
		}
	}
}
