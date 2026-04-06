using System;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Direct2D1;
using Vortice.Direct3D11;
using Vortice.DirectWrite;
using Vortice.DXGI;
using Vortice.Mathematics;

using DWriteFontStyle = Vortice.DirectWrite.FontStyle;
using DWriteFontWeight = Vortice.DirectWrite.FontWeight;
using DWriteFontStretch = Vortice.DirectWrite.FontStretch;
using AlphaMode = Vortice.DCommon.AlphaMode;
using D2DPixelFormat = Vortice.DCommon.PixelFormat;

namespace FDK
{
	/// <summary>
	/// DirectWrite-based font renderer. Renders text directly to D3D11 textures
	/// at physical resolution for sharp text at any display size.
	/// Replaces the GDI+-based CPrivateFont for higher quality text rendering.
	/// </summary>
	public class CDirectWriteFont : IDisposable
	{
		#region Shared factories

		private static ID2D1Factory? s_d2dFactory;
		private static IDWriteFactory? s_dwriteFactory;

		/// <summary>
		/// Initialize the shared Direct2D and DirectWrite factories.
		/// Call once at app startup before creating any CDirectWriteFont instances.
		/// </summary>
		public static void Initialize()
		{
			s_d2dFactory ??= D2D1.D2D1CreateFactory<ID2D1Factory>(Vortice.Direct2D1.FactoryType.SingleThreaded);
			s_dwriteFactory ??= DWrite.DWriteCreateFactory<IDWriteFactory>();
		}

		/// <summary>
		/// Release shared factories. Call at app shutdown.
		/// </summary>
		public static void Shutdown()
		{
			s_d2dFactory?.Dispose();
			s_d2dFactory = null;
			s_dwriteFactory?.Dispose();
			s_dwriteFactory = null;
		}

		#endregion

		#region Public types

		public enum DrawMode
		{
			/// <summary>Solid color text, no outline.</summary>
			Normal = 0,
			/// <summary>Outlined text with solid color fill.</summary>
			Edge = 1,
			/// <summary>Outlined text with vertical gradient fill.</summary>
			Gradation = 2,
		}

		#endregion

		#region Instance state

		private IDWriteTextFormat _textFormat;
		private float _logicalSizeDIP;   // Font size in DIP at logical resolution
		private float _physicalSizeDIP;  // Font size in DIP at physical resolution (scaled by fScreenRatio)

		#endregion

		#region Constructor

		/// <summary>
		/// Create a DirectWrite font for text rendering.
		/// The font renders at physical resolution (scaled by CTexture.fScreenRatio)
		/// but reports logical dimensions so the drawing pipeline works correctly.
		/// </summary>
		/// <param name="fontFamilyName">System font family name (e.g., "MS PGothic").</param>
		/// <param name="fontSizePt">Logical font size in points.</param>
		public CDirectWriteFont( string fontFamilyName, float fontSizePt )
		{
			Initialize();

			// Convert points to DIP (1 DIP = 1/96 inch, 1 pt = 1/72 inch)
			_logicalSizeDIP = fontSizePt * 96f / 72f;
			float scale = Math.Max( CTexture.fScreenRatio, 1f );
			_physicalSizeDIP = _logicalSizeDIP * scale;

			_textFormat = s_dwriteFactory!.CreateTextFormat(
				fontFamilyName,
				null, // system font collection
				DWriteFontWeight.Normal,
				DWriteFontStyle.Normal,
				DWriteFontStretch.Normal,
				_physicalSizeDIP
			);
			_textFormat.WordWrapping = WordWrapping.NoWrap;
		}

		#endregion

		#region Public methods

		/// <summary>
		/// Render text to a CTexture. The texture is at physical resolution but
		/// reports logical dimensions for correct positioning with fScreenRatio scaling.
		/// </summary>
		/// <returns>A CTexture ready for tDraw2D, or null if text is empty.</returns>
		public CTexture? RenderToTexture(
			ID3D11Device device,
			string text,
			DrawMode mode,
			System.Drawing.Color fontColor,
			System.Drawing.Color edgeColor,
			System.Drawing.Color? gradTopColor = null,
			System.Drawing.Color? gradBottomColor = null )
		{
			if ( string.IsNullOrEmpty( text ) ) return null;

			float scale = Math.Max( CTexture.fScreenRatio, 1f );
			float edgeSize = ( mode == DrawMode.Normal ) ? 0 : _physicalSizeDIP / 6f;
			float padding = edgeSize + 2f;

			// Measure text at physical size
			using var layout = s_dwriteFactory!.CreateTextLayout(
				text, _textFormat, 100000f, 100000f );
			var metrics = layout.Metrics;

			int texW = (int)Math.Ceiling( metrics.WidthIncludingTrailingWhitespace + padding * 2 );
			int texH = (int)Math.Ceiling( metrics.Height + padding * 2 );
			if ( texW <= 0 || texH <= 0 ) return null;

			Trace.TraceInformation(
				"[CDirectWriteFont] Rendering \"{0}\" at {1:F0}pt (physical {2:F0} DIP, scale {3:F2}), texture {4}x{5}",
				text.Length > 30 ? text.Substring( 0, 30 ) + "..." : text,
				_logicalSizeDIP * 72f / 96f, _physicalSizeDIP, scale, texW, texH );

			// Render to D3D11 texture via D2D1
			var srt = RenderTextToTexture( device, text, mode, texW, texH, padding, edgeSize,
				fontColor, edgeColor, gradTopColor ?? fontColor, gradBottomColor ?? fontColor );
			if ( srt == null ) return null;

			// Compute logical dimensions (physical ÷ scale)
			int logicalW = (int)Math.Ceiling( texW / scale );
			int logicalH = (int)Math.Ceiling( texH / scale );

			return new CTexture( srt, logicalW, logicalH );
		}

		#endregion

		#region Private rendering

		private ShaderResourceTexture? RenderTextToTexture(
			ID3D11Device device, string text, DrawMode mode,
			int texW, int texH, float padding, float edgeSize,
			System.Drawing.Color fontColor, System.Drawing.Color edgeColor,
			System.Drawing.Color gradTop, System.Drawing.Color gradBottom )
		{
			// 1. Create render target texture (D2D1 needs RenderTarget bind flag)
			var texDesc = new Texture2DDescription
			{
				Width = (uint)texW,
				Height = (uint)texH,
				MipLevels = 1,
				ArraySize = 1,
				Format = Format.B8G8R8A8_UNorm,
				SampleDescription = new SampleDescription( 1, 0 ),
				Usage = ResourceUsage.Default,
				BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
			};
			var renderTex = device.CreateTexture2D( texDesc );

			try
			{
				// 2. Create D2D1 render target on the DXGI surface
				using var surface = renderTex.QueryInterface<IDXGISurface>();
				var rtProps = new RenderTargetProperties
				{
					Type = RenderTargetType.Default,
					PixelFormat = new D2DPixelFormat( Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied ),
				};
				using var rt = s_d2dFactory!.CreateDxgiSurfaceRenderTarget( surface, rtProps );

				// 3. Configure antialiasing
				rt.AntialiasMode = AntialiasMode.PerPrimitive;
				rt.TextAntialiasMode = Vortice.Direct2D1.TextAntialiasMode.Grayscale;

				// 4. Draw
				using var drawLayout = s_dwriteFactory!.CreateTextLayout(
					text, _textFormat, texW, texH );

				rt.BeginDraw();
				rt.Clear( new Color4( 0, 0, 0, 0 ) );

				switch ( mode )
				{
					case DrawMode.Normal:
						DrawNormal( rt, drawLayout, padding, fontColor );
						break;
					case DrawMode.Edge:
						DrawEdge( rt, drawLayout, padding, edgeSize, fontColor, edgeColor );
						break;
					case DrawMode.Gradation:
						DrawGradient( rt, drawLayout, padding, edgeSize, texH, edgeColor, gradTop, gradBottom );
						break;
				}

				rt.EndDraw();

				// 5. Convert premultiplied alpha → straight alpha for compatibility
				//    with the existing SpriteBatch SrcAlpha blend state
				var result = ConvertPremultipliedToStraight( device, renderTex, texW, texH );
				return result;
			}
			finally
			{
				renderTex.Dispose();
			}
		}

		private void DrawNormal( ID2D1RenderTarget rt, IDWriteTextLayout layout,
			float padding, System.Drawing.Color color )
		{
			using var brush = rt.CreateSolidColorBrush( ToColor4( color ) );
			rt.DrawTextLayout( new Vector2( padding, padding ), layout, brush );
		}

		private void DrawEdge( ID2D1RenderTarget rt, IDWriteTextLayout layout,
			float padding, float edgeSize, System.Drawing.Color fontColor, System.Drawing.Color edgeColor )
		{
			using var edgeBrush = rt.CreateSolidColorBrush( ToColor4( edgeColor ) );
			using var fillBrush = rt.CreateSolidColorBrush( ToColor4( fontColor ) );

			// Draw outline by rendering text at circular offsets
			DrawTextOutline( rt, layout, padding, edgeSize, edgeBrush );

			// Draw fill on top
			rt.DrawTextLayout( new Vector2( padding, padding ), layout, fillBrush );
		}

		private void DrawGradient( ID2D1RenderTarget rt, IDWriteTextLayout layout,
			float padding, float edgeSize, int texHeight,
			System.Drawing.Color edgeColor, System.Drawing.Color gradTop, System.Drawing.Color gradBottom )
		{
			using var edgeBrush = rt.CreateSolidColorBrush( ToColor4( edgeColor ) );

			// Draw outline
			DrawTextOutline( rt, layout, padding, edgeSize, edgeBrush );

			// Draw gradient fill
			var gradStops = new GradientStop[]
			{
				new GradientStop( 0f, ToColor4( gradTop ) ),
				new GradientStop( 1f, ToColor4( gradBottom ) ),
			};
			using var gradCollection = rt.CreateGradientStopCollection( gradStops );
			using var gradBrush = rt.CreateLinearGradientBrush(
				new LinearGradientBrushProperties
				{
					StartPoint = new Vector2( 0, padding ),
					EndPoint = new Vector2( 0, texHeight - padding ),
				},
				gradCollection );
			rt.DrawTextLayout( new Vector2( padding, padding ), layout, gradBrush );
		}

		/// <summary>
		/// Draw text outline by rendering at radial offsets around the center position.
		/// Produces a smooth edge similar to GDI+ GraphicsPath.DrawPath with a round pen.
		/// </summary>
		private void DrawTextOutline( ID2D1RenderTarget rt, IDWriteTextLayout layout,
			float padding, float edgeSize, ID2D1Brush edgeBrush )
		{
			// Use enough angular steps and radial steps for smooth coverage.
			// At typical edge sizes (2-8 DIP), 16 angles × ~4 radial steps = ~64 draws.
			int angularSteps = Math.Max( 16, (int)( edgeSize * 4 ) );
			float radialStep = Math.Max( 0.5f, edgeSize / 4f );

			for ( int i = 0; i < angularSteps; i++ )
			{
				float angle = MathF.PI * 2f * i / angularSteps;
				float cos = MathF.Cos( angle );
				float sin = MathF.Sin( angle );

				for ( float r = radialStep; r <= edgeSize; r += radialStep )
				{
					float dx = cos * r;
					float dy = sin * r;
					rt.DrawTextLayout( new Vector2( padding + dx, padding + dy ), layout, edgeBrush );
				}
			}
		}

		#endregion

		#region Premultiplied → straight alpha conversion

		/// <summary>
		/// Copy a premultiplied-alpha texture to a new immutable straight-alpha texture.
		/// This is necessary because D2D1 always outputs premultiplied alpha, but the
		/// SpriteBatch blend state expects straight alpha (SrcAlpha, InvSrcAlpha).
		/// </summary>
		private unsafe ShaderResourceTexture? ConvertPremultipliedToStraight(
			ID3D11Device device, ID3D11Texture2D source, int width, int height )
		{
			// Create staging texture for CPU read
			var stagingDesc = new Texture2DDescription
			{
				Width = (uint)width,
				Height = (uint)height,
				MipLevels = 1,
				ArraySize = 1,
				Format = Format.B8G8R8A8_UNorm,
				SampleDescription = new SampleDescription( 1, 0 ),
				Usage = ResourceUsage.Staging,
				CPUAccessFlags = CpuAccessFlags.Read,
			};
			using var staging = device.CreateTexture2D( stagingDesc );

			var context = device.ImmediateContext;
			context.CopyResource( staging, source );

			var mapped = context.Map( staging, 0, MapMode.Read );
			try
			{
				byte[] pixels = new byte[width * height * 4];

				for ( int y = 0; y < height; y++ )
				{
					byte* srcRow = (byte*)mapped.DataPointer + y * mapped.RowPitch;
					int dstRowOffset = y * width * 4;

					for ( int x = 0; x < width; x++ )
					{
						int si = x * 4;
						byte b = srcRow[si + 0];
						byte g = srcRow[si + 1];
						byte r = srcRow[si + 2];
						byte a = srcRow[si + 3];

						// Premultiplied → straight: divide RGB by A
						if ( a > 0 && a < 255 )
						{
							pixels[dstRowOffset + si + 0] = (byte)Math.Min( 255, b * 255 / a );
							pixels[dstRowOffset + si + 1] = (byte)Math.Min( 255, g * 255 / a );
							pixels[dstRowOffset + si + 2] = (byte)Math.Min( 255, r * 255 / a );
							pixels[dstRowOffset + si + 3] = a;
						}
						else
						{
							pixels[dstRowOffset + si + 0] = b;
							pixels[dstRowOffset + si + 1] = g;
							pixels[dstRowOffset + si + 2] = r;
							pixels[dstRowOffset + si + 3] = a;
						}
					}
				}

				// Create immutable texture from straight-alpha pixel data
				fixed ( byte* pPixels = pixels )
				{
					var initData = new SubresourceData
					{
						DataPointer = (IntPtr)pPixels,
						RowPitch = (uint)( width * 4 ),
					};
					var desc = new Texture2DDescription
					{
						Width = (uint)width,
						Height = (uint)height,
						MipLevels = 1,
						ArraySize = 1,
						Format = Format.B8G8R8A8_UNorm,
						SampleDescription = new SampleDescription( 1, 0 ),
						Usage = ResourceUsage.Immutable,
						BindFlags = BindFlags.ShaderResource,
					};
					var tex = device.CreateTexture2D( desc, new[] { initData } );
					var srv = device.CreateShaderResourceView( tex );
					return new ShaderResourceTexture { Texture2D = tex, SRV = srv };
				}
			}
			finally
			{
				context.Unmap( staging, 0 );
			}
		}

		#endregion

		#region Helpers

		private static Color4 ToColor4( System.Drawing.Color c )
			=> new( c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f );

		#endregion

		#region IDisposable

		public void Dispose()
		{
			_textFormat?.Dispose();
			_textFormat = null!;
		}

		#endregion
	}
}
