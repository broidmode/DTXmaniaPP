using System.Runtime.InteropServices;

namespace FDK;

/// <summary>
/// Vertex format for sprite rendering in D3D11.
/// Position (float3) + Color (float4 RGBA) + TexCoord (float2) = 36 bytes.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct SpriteVertex
{
	public Vector3 Position;
	public Vector4 Color;
	public Vector2 TexCoord;

	public static readonly int SizeInBytes = Marshal.SizeOf<SpriteVertex>();

	public SpriteVertex(Vector3 position, Vector4 color, Vector2 texCoord)
	{
		Position = position;
		Color = color;
		TexCoord = texCoord;
	}
}
