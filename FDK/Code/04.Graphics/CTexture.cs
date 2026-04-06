using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using Vortice.DXGI;

using Rectangle = System.Drawing.Rectangle;

namespace FDK
{
	/// <summary>
	/// Wraps a D3D11 texture and provides 2D/3D sprite drawing through SpriteBatch.
	/// Dispose must be called when done; the finalizer detects leaks.
	/// </summary>
	public class CTexture : IDisposable
	{
		// Properties
		public bool bAdditiveBlending
		{
			get;
			set; 
		}
		public float fZAxisRotation
		{
			get;
			set;
		}
		public int nTransparency
		{
			get
			{
				return this._Transparency;
			}
			set
			{
				if( value < 0 )
				{
					this._Transparency = 0;
				}
				else if( value > 0xff )
				{
					this._Transparency = 0xff;
				}
				else
				{
					this._Transparency = value;
				}
			}
		}
		public Size szTextureSize
		{
			get; 
			private set;
		}
		public Size szImageSize
		{
			get;
			protected set;
		}
		public Texture texture
		{
			get;
			private set;
		}
		public Vortice.DXGI.Format Format
		{
			get;
			protected set;
		}
		public Vector3 vcScaleRatio;
		public string filename;

		// Screen scale properties - set once per resolution change.
		public static Size szLogicalScreen = Size.Empty;
		public static Size szPhysicalScreen = Size.Empty;
		public static Rectangle rcPhysicalScreenDrawingArea = Rectangle.Empty;
		/// <summary>
		/// Physical / logical screen ratio. Multiply logical coords by this to get physical.
		/// </summary>
		public static float fScreenRatio = 1.0f;

		/// <summary>
		/// Static SpriteBatch reference - set during device initialization.
		/// </summary>
		public static SpriteBatch SpriteBatch;

		/// <summary>
		/// View and projection matrices for the 3D (rotation) draw path.
		/// Set in CDTXMania.LoadContent().
		/// </summary>
		public static Matrix4x4 ViewMatrix = Matrix4x4.Identity;
		public static Matrix4x4 ProjectionMatrix = Matrix4x4.Identity;

		// Constructors

		public CTexture()
		{
			this.szImageSize = new Size( 0, 0 );
			this.szTextureSize = new Size( 0, 0 );
			this._Transparency = 0xff;
			this.texture = null;
			this.bTextureDisposed = true;
			this.bAdditiveBlending = false;
			this.fZAxisRotation = 0f;
			this.vcScaleRatio = new Vector3( 1f, 1f, 1f );
			this.filename = "";
			this.Format = Vortice.DXGI.Format.B8G8R8A8_UNorm;
		}

		/// <summary>
		/// Creates a CTexture from a pre-made ShaderResourceTexture with explicit logical size.
		/// The actual GPU texture may be at a higher resolution (e.g., for resolution-aware
		/// text rendering), but the drawing pipeline uses the logical dimensions for
		/// positioning and fScreenRatio scaling.
		/// </summary>
		internal CTexture( ShaderResourceTexture texture, int logicalWidth, int logicalHeight )
			: this()
		{
			this.szImageSize = new Size( logicalWidth, logicalHeight );
			this.szTextureSize = new Size( logicalWidth, logicalHeight );
			this.rcFullImage = new Rectangle( 0, 0, logicalWidth, logicalHeight );
			this.texture = texture;
			this.bTextureDisposed = false;
		}
		
		/// <summary>
		/// Creates a texture from a Bitmap with black (0xFF000000) as transparent color key.
		/// </summary>
		public CTexture( Device device, Bitmap bitmap )
			: this()
		{
			try
			{
				this.szImageSize = new Size( bitmap.Width, bitmap.Height );
				this.szTextureSize = this.szImageSize;
				this.rcFullImage = new Rectangle( 0, 0, this.szImageSize.Width, this.szImageSize.Height );
				this.texture = CreateTextureFromBitmap( device, bitmap, true );
				this.bTextureDisposed = false;
			}
			catch ( Exception e )
			{
				this.Dispose();
				throw new CTextureCreateFailedException( "Failed to create texture from bitmap: " + e.Message );
			}
		}

		/// <summary>
		/// Creates an empty texture of the given dimensions.
		/// </summary>
		public CTexture( Device device, int width, int height )
			: this( device, width, height, false )
		{
		}

		/// <summary>
		/// Creates an empty texture. If dynamic=true, the texture can be written by the CPU
		/// (used for video frame targets).
		/// </summary>
		public CTexture( Device device, int width, int height, bool dynamic )
			: this()
		{
			try
			{
				this.szImageSize = new Size( width, height );
				this.szTextureSize = this.szImageSize;
				this.rcFullImage = new Rectangle( 0, 0, width, height );
				
				var desc = new Texture2DDescription
				{
					Width = (uint)width,
					Height = (uint)height,
					MipLevels = 1,
					ArraySize = 1,
					Format = Vortice.DXGI.Format.B8G8R8A8_UNorm,
					SampleDescription = new SampleDescription( 1, 0 ),
					Usage = dynamic ? ResourceUsage.Dynamic : ResourceUsage.Default,
					BindFlags = BindFlags.ShaderResource,
					CPUAccessFlags = dynamic ? CpuAccessFlags.Write : CpuAccessFlags.None,
				};
				var tex = device.CreateTexture2D( desc );
				var srv = device.CreateShaderResourceView( tex );
				this.texture = new ShaderResourceTexture { Texture2D = tex, SRV = srv };
				this.bTextureDisposed = false;
			}
			catch
			{
				this.Dispose();
				throw new CTextureCreateFailedException( string.Format( "Failed to create texture ({0}x{1})", width, height ) );
			}
		}

		/// <summary>
		/// Creates a texture from an image file (BMP, JPG, PNG, etc.).
		/// </summary>
		public CTexture( Device device, string filename, bool colorKey )
			: this()
		{
			MakeTexture( device, filename, colorKey );
		}

		/// <summary>
		/// Creates a texture from image data in memory.
		/// </summary>
		public CTexture( Device device, byte[] txData, bool colorKey )
			: this()
		{
			MakeTexture( device, txData, colorKey );
		}

		/// <summary>
		/// Creates a texture from a Bitmap with optional color key.
		/// </summary>
		public CTexture( Device device, Bitmap bitmap, bool colorKey )
			: this()
		{
			MakeTexture( device, bitmap, colorKey );
		}

		// MakeTexture methods (for re-creating on existing CTexture instance)

		public void MakeTexture( Device device, string filename, bool colorKey )
		{
			if ( !File.Exists( filename ) )
				throw new FileNotFoundException( string.Format( "File not found: [{0}]", filename ) );

			byte[] txData = File.ReadAllBytes( filename );
			this.filename = Path.GetFileName( filename );
			MakeTexture( device, txData, colorKey );
		}

		public void MakeTexture( Device device, byte[] txData, bool colorKey )
		{
			try
			{
				using ( var ms = new MemoryStream( txData ) )
				using ( var bitmap = new Bitmap( ms ) )
				{
					this.szImageSize = new Size( bitmap.Width, bitmap.Height );
					this.szTextureSize = this.szImageSize;
					this.rcFullImage = new Rectangle( 0, 0, this.szImageSize.Width, this.szImageSize.Height );
					this.texture = CreateTextureFromBitmap( device, bitmap, colorKey );
					this.bTextureDisposed = false;
				}
			}
			catch ( Exception ex )
			{
				Trace.TraceError( "MakeTexture(byte[]) failed: {0}\n{1}", ex.Message, ex.StackTrace );
				this.Dispose();
				throw new CTextureCreateFailedException( "Failed to create texture: " + ex.Message );
			}
		}

		public void MakeTexture( Device device, Bitmap bitmap, bool colorKey )
		{
			try
			{
				this.szImageSize = new Size( bitmap.Width, bitmap.Height );
				this.szTextureSize = this.szImageSize;
				this.rcFullImage = new Rectangle( 0, 0, this.szImageSize.Width, this.szImageSize.Height );
				this.texture = CreateTextureFromBitmap( device, bitmap, colorKey );
				this.bTextureDisposed = false;
			}
			catch ( Exception ex )
			{
				Trace.TraceError( "MakeTexture(Bitmap) failed: {0}\n{1}", ex.Message, ex.StackTrace );
				this.Dispose();
				throw new CTextureCreateFailedException( "Failed to create texture: " + ex.Message );
			}
		}

		// Draw methods

		public void tDraw2D( Device device, int x, int y )
		{
			this.tDraw2D( device, x, y, 1f, this.rcFullImage );
		}
		public void tDraw2D( Device device, int x, int y, Rectangle rc )
		{
			this.tDraw2D( device, x, y, 1f, rc );
		}
		public void tDraw2D( Device device, float x, float y )
		{
			this.tDraw2D( device, (int) x, (int) y, 1f, this.rcFullImage );
		}
		public void tDraw2D( Device device, float x, float y, Rectangle rc )
		{
			this.tDraw2D( device, (int) x, (int) y, 1f, rc );
		}
		public void tDraw2D( Device device, int x, int y, float depth, Rectangle rc )
		{
			if ( this.texture == null || SpriteBatch == null )
				return;

			if ( this.fZAxisRotation == 0f )
			{
				// No rotation: screen-space quad
				float fx = x * fScreenRatio + rcPhysicalScreenDrawingArea.X;
				float fy = y * fScreenRatio + rcPhysicalScreenDrawingArea.Y;
				float w = rc.Width * this.vcScaleRatio.X * fScreenRatio;
				float h = rc.Height * this.vcScaleRatio.Y * fScreenRatio;
				float uL = (float) rc.Left / (float) this.szTextureSize.Width;
				float uR = (float) rc.Right / (float) this.szTextureSize.Width;
				float vT = (float) rc.Top / (float) this.szTextureSize.Height;
				float vB = (float) rc.Bottom / (float) this.szTextureSize.Height;
				float alpha = (float) this._Transparency / 255f;
				var color = new Vector4( 1f, 1f, 1f, alpha );

				if ( this.spriteQuad == null )
					this.spriteQuad = new SpriteVertex[ 4 ];

				this.spriteQuad[ 0 ] = new SpriteVertex( new Vector3( fx, fy, depth ), color, new Vector2( uL, vT ) );
				this.spriteQuad[ 1 ] = new SpriteVertex( new Vector3( fx + w, fy, depth ), color, new Vector2( uR, vT ) );
				this.spriteQuad[ 2 ] = new SpriteVertex( new Vector3( fx, fy + h, depth ), color, new Vector2( uL, vB ) );
				this.spriteQuad[ 3 ] = new SpriteVertex( new Vector3( fx + w, fy + h, depth ), color, new Vector2( uR, vB ) );

				SpriteBatch.Draw( this.texture.SRV, this.spriteQuad, this.bAdditiveBlending );
			}
			else
			{
				// Rotation: world-space quad centered at origin, transformed by WVP
				float halfW = (float) rc.Width / 2f;
				float halfH = (float) rc.Height / 2f;
				float uL = (float) rc.Left / (float) this.szTextureSize.Width;
				float uR = (float) rc.Right / (float) this.szTextureSize.Width;
				float vT = (float) rc.Top / (float) this.szTextureSize.Height;
				float vB = (float) rc.Bottom / (float) this.szTextureSize.Height;
				float alpha = (float) this._Transparency / 255f;
				var color = new Vector4( 1f, 1f, 1f, alpha );

				if ( this.spriteQuad == null )
					this.spriteQuad = new SpriteVertex[ 4 ];

				this.spriteQuad[ 0 ] = new SpriteVertex( new Vector3( -halfW, halfH, depth ), color, new Vector2( uL, vT ) );
				this.spriteQuad[ 1 ] = new SpriteVertex( new Vector3( halfW, halfH, depth ), color, new Vector2( uR, vT ) );
				this.spriteQuad[ 2 ] = new SpriteVertex( new Vector3( -halfW, -halfH, depth ), color, new Vector2( uL, vB ) );
				this.spriteQuad[ 3 ] = new SpriteVertex( new Vector3( halfW, -halfH, depth ), color, new Vector2( uR, vB ) );

				int cx = x + ( rc.Width / 2 );
				int cy = y + ( rc.Height / 2 );
				var translation = new Vector3(
					cx - ( (float) SampleFramework.GameWindowSize.Width / 2f ),
					-( cy - ( (float) SampleFramework.GameWindowSize.Height / 2f ) ),
					0f );

				var world = Matrix4x4.Identity * Matrix4x4.CreateScale( this.vcScaleRatio );
				world *= Matrix4x4.CreateRotationZ( this.fZAxisRotation );
				world *= Matrix4x4.CreateTranslation( translation );
				var wvp = world * ViewMatrix * ProjectionMatrix;

				SpriteBatch.Draw3D( this.texture.SRV, this.spriteQuad, this.bAdditiveBlending, wvp );
			}
		}

		public void tDraw2DUpsideDown( Device device, int x, int y )
		{
			this.tDraw2DUpsideDown( device, x, y, 1f, this.rcFullImage );
		}
		public void tDraw2DUpsideDown( Device device, int x, int y, Rectangle rc )
		{
			this.tDraw2DUpsideDown( device, x, y, 1f, rc );
		}
		public void tDraw2DUpsideDown( Device device, int x, int y, float depth, Rectangle rc )
		{
			if ( this.texture == null || SpriteBatch == null )
				return;

			float fx = x * fScreenRatio + rcPhysicalScreenDrawingArea.X;
			float fy = y * fScreenRatio + rcPhysicalScreenDrawingArea.Y;
			float w = rc.Width * this.vcScaleRatio.X * fScreenRatio;
			float h = rc.Height * this.vcScaleRatio.Y * fScreenRatio;
			float uL = (float) rc.Left / (float) this.szTextureSize.Width;
			float uR = (float) rc.Right / (float) this.szTextureSize.Width;
			float vT = (float) rc.Top / (float) this.szTextureSize.Height;
			float vB = (float) rc.Bottom / (float) this.szTextureSize.Height;
			float alpha = (float) this._Transparency / 255f;
			var color = new Vector4( 1f, 1f, 1f, alpha );

			if ( this.spriteQuad == null )
				this.spriteQuad = new SpriteVertex[ 4 ];

			// Flipped V coordinates: top-left gets bottom V, bottom-left gets top V
			this.spriteQuad[ 0 ] = new SpriteVertex( new Vector3( fx, fy, depth ), color, new Vector2( uL, vB ) );
			this.spriteQuad[ 1 ] = new SpriteVertex( new Vector3( fx + w, fy, depth ), color, new Vector2( uR, vB ) );
			this.spriteQuad[ 2 ] = new SpriteVertex( new Vector3( fx, fy + h, depth ), color, new Vector2( uL, vT ) );
			this.spriteQuad[ 3 ] = new SpriteVertex( new Vector3( fx + w, fy + h, depth ), color, new Vector2( uR, vT ) );

			SpriteBatch.Draw( this.texture.SRV, this.spriteQuad, this.bAdditiveBlending );
		}

		public void tDraw3D( Device device, Matrix mat )
		{
			this.tDraw3D( device, mat, this.rcFullImage );
		}
		public void tDraw3D( Device device, Matrix mat, Rectangle rc )
		{
			if ( this.texture == null || SpriteBatch == null )
				return;

			float halfW = (float) rc.Width / 2f;
			float halfH = (float) rc.Height / 2f;
			float uL = (float) rc.Left / (float) this.szTextureSize.Width;
			float uR = (float) rc.Right / (float) this.szTextureSize.Width;
			float vT = (float) rc.Top / (float) this.szTextureSize.Height;
			float vB = (float) rc.Bottom / (float) this.szTextureSize.Height;
			float alpha = (float) this._Transparency / 255f;
			var color = new Vector4( 1f, 1f, 1f, alpha );

			if ( this.spriteQuad == null )
				this.spriteQuad = new SpriteVertex[ 4 ];

			this.spriteQuad[ 0 ] = new SpriteVertex( new Vector3( -halfW, halfH, 0f ), color, new Vector2( uL, vT ) );
			this.spriteQuad[ 1 ] = new SpriteVertex( new Vector3( halfW, halfH, 0f ), color, new Vector2( uR, vT ) );
			this.spriteQuad[ 2 ] = new SpriteVertex( new Vector3( -halfW, -halfH, 0f ), color, new Vector2( uL, vB ) );
			this.spriteQuad[ 3 ] = new SpriteVertex( new Vector3( halfW, -halfH, 0f ), color, new Vector2( uR, vB ) );

			var wvp = mat * ViewMatrix * ProjectionMatrix;
			SpriteBatch.Draw3D( this.texture.SRV, this.spriteQuad, this.bAdditiveBlending, wvp );
		}

		public void tDraw3DTopLeftReference( Device device, Matrix mat )
		{
			this.tDraw3DTopLeftReference( device, mat, this.rcFullImage );
		}
		public void tDraw3DTopLeftReference( Device device, Matrix mat, Rectangle rc )
		{
			if ( this.texture == null || SpriteBatch == null )
				return;

			float w = (float) rc.Width;
			float h = (float) rc.Height;
			float uL = (float) rc.Left / (float) this.szTextureSize.Width;
			float uR = (float) rc.Right / (float) this.szTextureSize.Width;
			float vT = (float) rc.Top / (float) this.szTextureSize.Height;
			float vB = (float) rc.Bottom / (float) this.szTextureSize.Height;
			float alpha = (float) this._Transparency / 255f;
			var color = new Vector4( 1f, 1f, 1f, alpha );

			if ( this.spriteQuad == null )
				this.spriteQuad = new SpriteVertex[ 4 ];

			// Top-left reference: vertices start at origin, extend right/down
			this.spriteQuad[ 0 ] = new SpriteVertex( new Vector3( 0f, 0f, 0f ), color, new Vector2( uL, vT ) );
			this.spriteQuad[ 1 ] = new SpriteVertex( new Vector3( w, 0f, 0f ), color, new Vector2( uR, vT ) );
			this.spriteQuad[ 2 ] = new SpriteVertex( new Vector3( 0f, -h, 0f ), color, new Vector2( uL, vB ) );
			this.spriteQuad[ 3 ] = new SpriteVertex( new Vector3( w, -h, 0f ), color, new Vector2( uR, vB ) );

			var wvp = mat * ViewMatrix * ProjectionMatrix;
			SpriteBatch.Draw3D( this.texture.SRV, this.spriteQuad, this.bAdditiveBlending, wvp );
		}

		#region [ IDisposable ]
		//-----------------
		public void Dispose()
		{
			this.Dispose( true );
			GC.SuppressFinalize( this );
		}
		protected void Dispose( bool disposeManagedObjects )
		{
			if ( this.bDisposed )
				return;

			if ( disposeManagedObjects )
			{
				if ( this.texture != null )
				{
					this.texture.Dispose();
					this.texture = null;
					this.bTextureDisposed = true;
				}
			}

			this.bDisposed = true;
		}
		~CTexture()
		{
			if ( !this.bTextureDisposed )
			{
				Trace.TraceWarning( "CTexture: Dispose leak detected. (Size=({0}, {1}), filename={2})", szImageSize.Width, szImageSize.Height, filename );
			}
			this.Dispose( false );
		}
		//-----------------
		#endregion


		#region [ private ]
		//-----------------
		private int _Transparency;
		private bool bDisposed, bTextureDisposed;
		private SpriteVertex[] spriteQuad;
		static object lockobj = new object();

		protected Rectangle rcFullImage;
		protected Color4 color4 = new Color4( 1f, 1f, 1f, 1f );

		/// <summary>
		/// Creates a D3D11 texture from a System.Drawing.Bitmap.
		/// Optionally applies a color key making black (RGB=0,0,0) pixels transparent.
		/// </summary>
		private static unsafe ShaderResourceTexture CreateTextureFromBitmap( Device device, Bitmap bitmap, bool colorKey )
		{
			int w = bitmap.Width;
			int h = bitmap.Height;

			BitmapData bmpData = bitmap.LockBits(
				new Rectangle( 0, 0, w, h ),
				ImageLockMode.ReadOnly,
				PixelFormat.Format32bppArgb );

			try
			{
				if ( colorKey )
				{
					// Copy pixels and apply color key
					int byteCount = h * bmpData.Stride;
					byte[] pixels = new byte[ byteCount ];
					Marshal.Copy( bmpData.Scan0, pixels, 0, byteCount );
					ApplyColorKey( pixels );

					fixed ( byte* pPixels = pixels )
					{
						var initData = new SubresourceData( (IntPtr) pPixels, (uint)bmpData.Stride );
						return CreateSRTexture( device, w, h, initData );
					}
				}
				else
				{
					var initData = new SubresourceData( bmpData.Scan0, (uint)bmpData.Stride );
					return CreateSRTexture( device, w, h, initData );
				}
			}
			finally
			{
				bitmap.UnlockBits( bmpData );
			}
		}

		private static ShaderResourceTexture CreateSRTexture( Device device, int w, int h, SubresourceData initData )
		{
			var desc = new Texture2DDescription
			{
				Width = (uint)w,
				Height = (uint)h,
				MipLevels = 1,
				ArraySize = 1,
				Format = Vortice.DXGI.Format.B8G8R8A8_UNorm,
				SampleDescription = new SampleDescription( 1, 0 ),
				Usage = ResourceUsage.Immutable,
				BindFlags = BindFlags.ShaderResource,
			};
			var tex = device.CreateTexture2D( desc, new[] { initData } );
			var srv = device.CreateShaderResourceView( tex );
			return new ShaderResourceTexture { Texture2D = tex, SRV = srv };
		}

		/// <summary>
		/// Makes pure black pixels (R=0, G=0, B=0) fully transparent.
		/// Pixel data is in BGRA format (Format32bppArgb on little-endian x86).
		/// </summary>
		private static unsafe void ApplyColorKey( byte[] pixels )
		{
			fixed ( byte* p = pixels )
			{
				for ( int i = 0; i < pixels.Length; i += 4 )
				{
					// BGRA layout: p[i]=B, p[i+1]=G, p[i+2]=R, p[i+3]=A
					if ( p[ i ] == 0 && p[ i + 1 ] == 0 && p[ i + 2 ] == 0 )
					{
						p[ i + 3 ] = 0; // Set alpha to transparent
					}
				}
			}
		}
		//-----------------
		#endregion
	}
}
