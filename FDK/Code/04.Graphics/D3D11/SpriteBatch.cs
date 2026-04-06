using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace FDK;

/// <summary>
/// Batched 2D sprite renderer for D3D11.
/// Accumulates quads during the frame, then flushes them in minimal draw calls
/// grouped by texture and blend state.
/// </summary>
public class SpriteBatch : IDisposable
{
	#region Shader Source

	private const string ShaderSource = @"
cbuffer Constants : register(b0)
{
	row_major float4x4 MatrixTransform;
};

struct VSInput
{
	float3 Position : POSITION;
	float4 Color    : COLOR;
	float2 TexCoord : TEXCOORD;
};

struct PSInput
{
	float4 Position : SV_POSITION;
	float4 Color    : COLOR;
	float2 TexCoord : TEXCOORD;
};

Texture2D    SpriteTexture : register(t0);
SamplerState SpriteSampler : register(s0);

PSInput VSMain(VSInput input)
{
	PSInput output;
	output.Position = mul(float4(input.Position, 1.0), MatrixTransform);
	output.Color    = input.Color;
	output.TexCoord = input.TexCoord;
	return output;
}

float4 PSMain(PSInput input) : SV_TARGET
{
	float4 texColor = SpriteTexture.Sample(SpriteSampler, input.TexCoord);
	float4 result = texColor * input.Color;
	clip(result.a - (10.0 / 255.0));
	return result;
}
";

	#endregion

	#region Constants

	private const int DefaultMaxSprites = 2048;

	#endregion

	#region GPU Resources

	private ID3D11Device _device;
	private ID3D11DeviceContext _context;

	private ID3D11VertexShader _vertexShader;
	private ID3D11PixelShader _pixelShader;
	private ID3D11InputLayout _inputLayout;

	private ID3D11Buffer _vertexBuffer;
	private ID3D11Buffer _indexBuffer;
	private ID3D11Buffer _constantBuffer;

	private ID3D11SamplerState _samplerLinear;
	private ID3D11BlendState _blendAlpha;
	private ID3D11BlendState _blendAdditive;
	private ID3D11RasterizerState _rasterizerState;
	private ID3D11DepthStencilState _depthStencilState;

	// Render target and viewport must be re-bound each frame before DrawIndexed.
	// ClearRenderTargetView uses an explicit RTV parameter, but DrawIndexed renders
	// to whatever is OM-bound — which can be lost between frames.
	private ID3D11RenderTargetView? _renderTargetView;
	private int _viewportWidth;
	private int _viewportHeight;

	#endregion

	#region Batching State

	private struct BatchInfo
	{
		public ID3D11ShaderResourceView Texture;
		public bool Additive;
		public Matrix4x4 Transform;
		public int SpriteOffset;
		public int SpriteCount;
	}

	private SpriteVertex[] _vertices;
	private int _spriteCount;
	private int _maxSprites;
	private readonly List<BatchInfo> _batches = new();
	private bool _begun;

	// Current batch tracking
	private ID3D11ShaderResourceView? _currentTexture;
	private bool _currentAdditive;
	private Matrix4x4 _currentTransform;
	private Matrix4x4 _projectionMatrix; // The 2D ortho projection, saved from Begin()

	#endregion

	public SpriteBatch(ID3D11Device device, ID3D11DeviceContext context)
	{
		_device = device;
		_context = context;
		_maxSprites = DefaultMaxSprites;
		_vertices = new SpriteVertex[_maxSprites * 4];

		CompileShaders();
		CreateBuffers();
		CreateStates();
	}

	/// <summary>
	/// Begin a sprite batch frame. Call once before submitting draws.
	/// </summary>
	/// <param name="projection">Orthographic projection for 2D draws.</param>
	/// <param name="renderTargetView">The render target to draw into.</param>
	/// <param name="viewportWidth">Viewport width in pixels.</param>
	/// <param name="viewportHeight">Viewport height in pixels.</param>
	public void Begin(Matrix4x4 projection, ID3D11RenderTargetView renderTargetView, int viewportWidth, int viewportHeight)
	{
		_projectionMatrix = projection;
		_currentTransform = projection;
		_renderTargetView = renderTargetView;
		_viewportWidth = viewportWidth;
		_viewportHeight = viewportHeight;
		_spriteCount = 0;
		_batches.Clear();
		_currentTexture = null;
		_begun = true;
	}

	/// <summary>
	/// Submit a textured quad for drawing (2D path, uses the current projection).
	/// Vertices should be in screen space (scaled by fScreenRatio).
	/// </summary>
	public void Draw(ID3D11ShaderResourceView? texture, SpriteVertex[] quad, bool additive)
	{
		if (texture == null) return;
		if (!_begun) return;

		// 2D draws always use the ortho projection. If the previous batch was
		// a 3D draw (Draw3D), we must start a new batch to restore the ortho
		// transform, even if the texture and blend state match.
		bool needNewBatch = _batches.Count == 0
			|| _currentTexture != texture
			|| _currentAdditive != additive
			|| _currentTransform != _projectionMatrix;

		if (needNewBatch)
			StartNewBatch(texture, additive, _projectionMatrix);

		AppendQuad(quad);
	}

	/// <summary>
	/// Submit a textured quad for 3D drawing with a custom world*view*projection transform.
	/// Always starts a new batch since the transform differs.
	/// </summary>
	public void Draw3D(ID3D11ShaderResourceView? texture, SpriteVertex[] quad, bool additive, Matrix4x4 transform)
	{
		if (texture == null) return;
		if (!_begun) return;

		StartNewBatch(texture, additive, transform);
		AppendQuad(quad);
	}

	/// <summary>
	/// Flush all accumulated draws to the GPU. Call once at end of frame.
	/// </summary>
	public void End()
	{
		if (!_begun) return;
		_begun = false;

		if (_spriteCount == 0)
			return;

		UploadVertices();
		SetPipelineState();
		DrawBatches();
	}

	#region Private Methods

	private void StartNewBatch(ID3D11ShaderResourceView texture, bool additive, Matrix4x4 transform)
	{
		_currentTexture = texture;
		_currentAdditive = additive;
		_currentTransform = transform;

		_batches.Add(new BatchInfo
		{
			Texture = texture,
			Additive = additive,
			Transform = transform,
			SpriteOffset = _spriteCount,
			SpriteCount = 0
		});
	}

	private void AppendQuad(SpriteVertex[] quad)
	{
		EnsureCapacity(_spriteCount + 1);

		int baseVertex = _spriteCount * 4;
		Array.Copy(quad, 0, _vertices, baseVertex, 4);
		_spriteCount++;

		// Update last batch count
		var last = _batches[^1];
		last.SpriteCount++;
		_batches[^1] = last;
	}

	private void EnsureCapacity(int spriteCount)
	{
		if (spriteCount <= _maxSprites) return;

		int newMax = _maxSprites;
		while (newMax < spriteCount) newMax *= 2;

		Array.Resize(ref _vertices, newMax * 4);

		// Recreate GPU buffers at new size
		_vertexBuffer?.Dispose();
		_indexBuffer?.Dispose();
		_maxSprites = newMax;
		CreateVertexBuffer();
		CreateIndexBuffer();
	}

	private unsafe void UploadVertices()
	{
		var mapped = _context.Map(_vertexBuffer, MapMode.WriteDiscard);
		try
		{
			fixed (SpriteVertex* src = _vertices)
			{
				int byteCount = _spriteCount * 4 * SpriteVertex.SizeInBytes;
				Buffer.MemoryCopy(src, mapped.DataPointer.ToPointer(),
					_maxSprites * 4 * SpriteVertex.SizeInBytes, byteCount);
			}
		}
		finally
		{
			_context.Unmap(_vertexBuffer, 0);
		}
	}

	private void SetPipelineState()
	{
		_context.IASetInputLayout(_inputLayout);
		_context.IASetVertexBuffer(0, _vertexBuffer, (uint)SpriteVertex.SizeInBytes);
		_context.IASetIndexBuffer(_indexBuffer, Format.R16_UInt, 0);
		_context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);

		_context.VSSetShader(_vertexShader);
		_context.VSSetConstantBuffer(0, _constantBuffer);

		_context.RSSetState(_rasterizerState);
		_context.RSSetViewport(0, 0, _viewportWidth, _viewportHeight);

		_context.PSSetShader(_pixelShader);
		_context.PSSetSampler(0, _samplerLinear);

		_context.OMSetRenderTargets(_renderTargetView);
		_context.OMSetDepthStencilState(_depthStencilState);
	}

	private void DrawBatches()
	{
		Matrix4x4 lastTransform = default;
		bool firstBatch = true;

		foreach (var batch in _batches)
		{
			if (batch.SpriteCount == 0) continue;

			// Update constant buffer if transform changed
			if (firstBatch || batch.Transform != lastTransform)
			{
				_context.UpdateSubresource(batch.Transform, _constantBuffer);
				lastTransform = batch.Transform;
				firstBatch = false;
			}

			// Set blend state
			_context.OMSetBlendState(batch.Additive ? _blendAdditive : _blendAlpha);

			// Set texture
			_context.PSSetShaderResource(0, batch.Texture);

			// Draw indexed
			int indexCount = batch.SpriteCount * 6;
			int startIndex = batch.SpriteOffset * 6;
			_context.DrawIndexed((uint)indexCount, (uint)startIndex, 0);
		}
	}

	#endregion

	#region Resource Creation

	private void CompileShaders()
	{
		ReadOnlyMemory<byte> vsBytecode = Compiler.Compile(ShaderSource, "VSMain", "SpriteBatch", "vs_5_0");
		_vertexShader = _device.CreateVertexShader(vsBytecode.Span);

		// Create input layout from VS bytecode
		InputElementDescription[] inputElements = new[]
		{
			new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
			new InputElementDescription("COLOR", 0, Format.R32G32B32A32_Float, 12, 0),
			new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 28, 0),
		};
		_inputLayout = _device.CreateInputLayout(inputElements, vsBytecode.Span);

		// Compile pixel shader
		ReadOnlyMemory<byte> psBytecode = Compiler.Compile(ShaderSource, "PSMain", "SpriteBatch", "ps_5_0");

		_pixelShader = _device.CreatePixelShader(psBytecode.Span);
	}

	private void CreateBuffers()
	{
		CreateVertexBuffer();
		CreateIndexBuffer();

		// Constant buffer (one Matrix4x4 = 64 bytes)
		var cbDesc = new BufferDescription
		{
			ByteWidth = 64, // sizeof(Matrix4x4)
			Usage = ResourceUsage.Default,
			BindFlags = BindFlags.ConstantBuffer,
		};
		_constantBuffer = _device.CreateBuffer(cbDesc);
	}

	private void CreateVertexBuffer()
	{
		var vbDesc = new BufferDescription
		{
			ByteWidth = (uint)(_maxSprites * 4 * SpriteVertex.SizeInBytes),
			Usage = ResourceUsage.Dynamic,
			BindFlags = BindFlags.VertexBuffer,
			CPUAccessFlags = CpuAccessFlags.Write,
		};
		_vertexBuffer = _device.CreateBuffer(vbDesc);
	}

	private void CreateIndexBuffer()
	{
		// Pre-generate indices for all possible quads
		// Each quad: 4 vertices → 6 indices (two triangles)
		// Vertex order: 0=TL, 1=TR, 2=BL, 3=BR
		// Triangle 1: 0,1,2  Triangle 2: 2,1,3
		ushort[] indices = new ushort[_maxSprites * 6];
		for (int i = 0; i < _maxSprites; i++)
		{
			int vi = i * 4;
			int ii = i * 6;
			indices[ii + 0] = (ushort)(vi + 0);
			indices[ii + 1] = (ushort)(vi + 1);
			indices[ii + 2] = (ushort)(vi + 2);
			indices[ii + 3] = (ushort)(vi + 2);
			indices[ii + 4] = (ushort)(vi + 1);
			indices[ii + 5] = (ushort)(vi + 3);
		}

		var ibDesc = new BufferDescription
		{
			ByteWidth = (uint)(indices.Length * sizeof(ushort)),
			Usage = ResourceUsage.Immutable,
			BindFlags = BindFlags.IndexBuffer,
		};
		unsafe
		{
			fixed (ushort* pIndices = indices)
			{
				var initData = new SubresourceData((IntPtr)pIndices, (uint)(indices.Length * sizeof(ushort)));
				_indexBuffer = _device.CreateBuffer(ibDesc, initData);
			}
		}
	}

	private void CreateStates()
	{
		// Sampler: linear filtering, clamp addressing
		var samplerDesc = new SamplerDescription
		{
			Filter = Filter.MinMagMipLinear,
			AddressU = TextureAddressMode.Clamp,
			AddressV = TextureAddressMode.Clamp,
			AddressW = TextureAddressMode.Clamp,
		};
		_samplerLinear = _device.CreateSamplerState(samplerDesc);

		// Blend state: alpha blend (SrcAlpha + InvSrcAlpha)
		var blendDesc = new BlendDescription();
		blendDesc.RenderTarget[0] = new RenderTargetBlendDescription
		{
			BlendEnable = true,
			SourceBlend = Blend.SourceAlpha,
			DestinationBlend = Blend.InverseSourceAlpha,
			BlendOperation = BlendOperation.Add,
			SourceBlendAlpha = Blend.One,
			DestinationBlendAlpha = Blend.InverseSourceAlpha,
			BlendOperationAlpha = BlendOperation.Add,
			RenderTargetWriteMask = ColorWriteEnable.All,
		};
		_blendAlpha = _device.CreateBlendState(blendDesc);

		// Blend state: additive (SrcAlpha + One)
		blendDesc.RenderTarget[0].DestinationBlend = Blend.One;
		blendDesc.RenderTarget[0].DestinationBlendAlpha = Blend.One;
		_blendAdditive = _device.CreateBlendState(blendDesc);

		// Rasterizer: no culling, solid fill
		var rastDesc = new RasterizerDescription
		{
			FillMode = FillMode.Solid,
			CullMode = CullMode.None,
			DepthClipEnable = true,
		};
		_rasterizerState = _device.CreateRasterizerState(rastDesc);

		// Depth stencil: disabled
		var dsDesc = new DepthStencilDescription
		{
			DepthEnable = false,
			DepthWriteMask = DepthWriteMask.Zero,
			StencilEnable = false,
		};
		_depthStencilState = _device.CreateDepthStencilState(dsDesc);
	}

	#endregion

	#region IDisposable

	public void Dispose()
	{
		_depthStencilState?.Dispose();
		_rasterizerState?.Dispose();
		_blendAdditive?.Dispose();
		_blendAlpha?.Dispose();
		_samplerLinear?.Dispose();
		_constantBuffer?.Dispose();
		_indexBuffer?.Dispose();
		_vertexBuffer?.Dispose();
		_inputLayout?.Dispose();
		_pixelShader?.Dispose();
		_vertexShader?.Dispose();
	}

	#endregion
}
