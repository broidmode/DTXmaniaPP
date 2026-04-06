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
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using FDK;

using Rectangle = System.Drawing.Rectangle;

namespace SampleFramework
{
	/// <summary>
	/// Handles the configuration and management of the D3D11 graphics device.
	/// </summary>
	public class GraphicsDeviceManager : IDisposable
	{
		Game game;
		bool ignoreSizeChanges;

		int fullscreenWindowWidth;
		int fullscreenWindowHeight;
		int windowedWindowWidth;
		int windowedWindowHeight;
		WINDOWPLACEMENT windowedPlacement;
		long windowedStyle;
		bool savedTopmost;

		public DeviceSettings CurrentSettings
		{
			get;
			private set;
		}
		public bool IsWindowed
		{
			get { return CurrentSettings.Windowed; }
		}
		public int ScreenWidth
		{
			get { return CurrentSettings.BackBufferWidth; }
		}
		public int ScreenHeight
		{
			get { return CurrentSettings.BackBufferHeight; }
		}
		public Size ScreenSize
		{
			get { return new Size(CurrentSettings.BackBufferWidth, CurrentSettings.BackBufferHeight); }
		}
		public CGraphicsDevice GraphicsDevice
		{
			get;
			private set;
		}
		public string DeviceStatistics
		{
			get;
			private set;
		}
		public string DeviceInformation
		{
			get;
			private set;
		}

		public GraphicsDeviceManager(Game game)
		{
			if (game == null)
				throw new ArgumentNullException("game");

			this.game = game;

			game.Window.ScreenChanged += Window_ScreenChanged;
			game.Window.UserResized += Window_UserResized;

			game.FrameStart += game_FrameStart;
			game.FrameEnd += game_FrameEnd;

			GraphicsDevice = new CGraphicsDevice(this);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		public void ChangeDevice(DeviceSettings settings, DeviceSettings minimumSettings)
		{
			if (settings == null)
				throw new ArgumentNullException("settings");

			CreateDevice(settings);
		}
		public void ChangeDevice(bool windowed, int desiredWidth, int desiredHeight)
		{
			DeviceSettings desiredSettings = new DeviceSettings();
			desiredSettings.Windowed = windowed;
			desiredSettings.BackBufferWidth = desiredWidth;
			desiredSettings.BackBufferHeight = desiredHeight;

			ChangeDevice(desiredSettings, null);
		}
		public void ChangeDevice(DeviceSettings settings)
		{
			ChangeDevice(settings, null);
		}

		public void ToggleFullScreen()
		{
			if (!EnsureDevice())
				throw new InvalidOperationException("No valid device.");

			DeviceSettings newSettings = CurrentSettings.Clone();

			newSettings.Windowed = !newSettings.Windowed;

			int width = newSettings.Windowed ? windowedWindowWidth : fullscreenWindowWidth;
			int height = newSettings.Windowed ? windowedWindowHeight : fullscreenWindowHeight;

			newSettings.BackBufferWidth = width;
			newSettings.BackBufferHeight = height;

			ChangeDevice(newSettings);
		}
		public bool EnsureDevice()
		{
			return GraphicsDevice.Device != null;
		}

		protected virtual void Dispose(bool disposing)
		{
			if (this.bDisposed)
				return;
			this.bDisposed = true;

			if (disposing)
				ReleaseDevice();
		}
		private bool bDisposed = false;

		void CreateDevice(DeviceSettings settings)
		{
			DeviceSettings oldSettings = CurrentSettings;
			CurrentSettings = settings;

			ignoreSizeChanges = true;

			bool keepCurrentWindowSize = false;
			if (settings.BackBufferWidth == 0 && settings.BackBufferHeight == 0)
				keepCurrentWindowSize = true;

			// Handle window state for fullscreen toggle
			if (settings.Windowed)
			{
				if (oldSettings != null && !oldSettings.Windowed)
					NativeMethods.SetWindowLong(game.Window.Handle, WindowConstants.GWL_STYLE, (uint)windowedStyle);
			}
			else
			{
				if (oldSettings == null || oldSettings.Windowed)
				{
					savedTopmost = game.Window.TopMost;
					long style = NativeMethods.GetWindowLong(game.Window.Handle, WindowConstants.GWL_STYLE);
					style &= ~WindowConstants.WS_MAXIMIZE & ~WindowConstants.WS_MINIMIZE;
					windowedStyle = style;

					windowedPlacement = new WINDOWPLACEMENT();
					windowedPlacement.length = WINDOWPLACEMENT.Length;
					NativeMethods.GetWindowPlacement(game.Window.Handle, ref windowedPlacement);
				}

				game.Window.Hide();
				NativeMethods.SetWindowLong(game.Window.Handle, WindowConstants.GWL_STYLE, (uint)(WindowConstants.WS_POPUP | WindowConstants.WS_SYSMENU));

				WINDOWPLACEMENT placement = new WINDOWPLACEMENT();
				placement.length = WINDOWPLACEMENT.Length;
				NativeMethods.GetWindowPlacement(game.Window.Handle, ref placement);

				if ((placement.flags & WindowConstants.WPF_RESTORETOMAXIMIZED) != 0)
				{
					placement.flags &= ~WindowConstants.WPF_RESTORETOMAXIMIZED;
					placement.showCmd = WindowConstants.SW_RESTORE;
					NativeMethods.SetWindowPlacement(game.Window.Handle, ref placement);
				}
			}

			// Track previous window sizes for fullscreen toggle
			if (settings.Windowed)
			{
				if (oldSettings != null && !oldSettings.Windowed)
				{
					fullscreenWindowWidth = oldSettings.BackBufferWidth;
					fullscreenWindowHeight = oldSettings.BackBufferHeight;
				}
			}
			else
			{
				if (oldSettings != null && oldSettings.Windowed)
				{
					windowedWindowWidth = oldSettings.BackBufferWidth;
					windowedWindowHeight = oldSettings.BackBufferHeight;
				}
			}

			// Create or resize the D3D11 device
			if (GraphicsDevice.Device == null)
			{
				InitializeDevice();
			}
			else
			{
				ResizeDevice();
			}

			UpdateDeviceInformation();

			// Restore window placement when switching from fullscreen to windowed
			if (oldSettings != null && !oldSettings.Windowed && settings.Windowed)
			{
				NativeMethods.SetWindowPlacement(game.Window.Handle, ref windowedPlacement);
				game.Window.TopMost = savedTopmost;
			}

			// Resize window to match requested back buffer size
			if (settings.Windowed && !keepCurrentWindowSize)
			{
				int width;
				int height;
				if (NativeMethods.IsIconic(game.Window.Handle))
				{
					WINDOWPLACEMENT placement = new WINDOWPLACEMENT();
					placement.length = WINDOWPLACEMENT.Length;
					NativeMethods.GetWindowPlacement(game.Window.Handle, ref placement);

					if ((placement.flags & WindowConstants.WPF_RESTORETOMAXIMIZED) != 0 && placement.showCmd == WindowConstants.SW_SHOWMINIMIZED)
					{
						NativeMethods.ShowWindow(game.Window.Handle, WindowConstants.SW_RESTORE);
						Rectangle rect = NativeMethods.GetClientRectangle(game.Window.Handle);
						width = rect.Width;
						height = rect.Height;
						NativeMethods.ShowWindow(game.Window.Handle, WindowConstants.SW_MINIMIZE);
					}
					else
					{
						NativeRectangle frame = new NativeRectangle();
						NativeMethods.AdjustWindowRect(ref frame, (uint)windowedStyle, false);
						int frameWidth = frame.right - frame.left;
						int frameHeight = frame.bottom - frame.top;
						width = placement.rcNormalPosition.right - placement.rcNormalPosition.left - frameWidth;
						height = placement.rcNormalPosition.bottom - placement.rcNormalPosition.top - frameHeight;
					}
				}
				else
				{
					Rectangle rect = NativeMethods.GetClientRectangle(game.Window.Handle);
					width = rect.Width;
					height = rect.Height;
				}

				if (width != settings.BackBufferWidth ||
					height != settings.BackBufferHeight)
				{
					if (NativeMethods.IsIconic(game.Window.Handle))
						NativeMethods.ShowWindow(game.Window.Handle, WindowConstants.SW_RESTORE);
					if (NativeMethods.IsZoomed(game.Window.Handle))
						NativeMethods.ShowWindow(game.Window.Handle, WindowConstants.SW_RESTORE);

					NativeRectangle rect = new NativeRectangle();
					rect.right = settings.BackBufferWidth;
					rect.bottom = settings.BackBufferHeight;
					NativeMethods.AdjustWindowRect(ref rect,
						NativeMethods.GetWindowLong(game.Window.Handle, WindowConstants.GWL_STYLE), false);

					NativeMethods.SetWindowPos(game.Window.Handle, IntPtr.Zero, 0, 0, rect.right - rect.left,
						rect.bottom - rect.top, WindowConstants.SWP_NOZORDER | WindowConstants.SWP_NOMOVE);

					Rectangle r = NativeMethods.GetClientRectangle(game.Window.Handle);
					int clientWidth = r.Width;
					int clientHeight = r.Height;

					if (clientWidth != settings.BackBufferWidth ||
						clientHeight != settings.BackBufferHeight)
					{
						DeviceSettings newSettings = CurrentSettings.Clone();
						newSettings.BackBufferWidth = clientWidth;
						newSettings.BackBufferHeight = clientHeight;
						CreateDevice(newSettings);
					}
				}
			}

			if (!game.Window.Visible)
				NativeMethods.ShowWindow(game.Window.Handle, WindowConstants.SW_SHOW);

			if (!IsWindowed)
				NativeMethods.SetThreadExecutionState(WindowConstants.ES_DISPLAY_REQUIRED | WindowConstants.ES_CONTINUOUS);
			else
				NativeMethods.SetThreadExecutionState(WindowConstants.ES_CONTINUOUS);

			ignoreSizeChanges = false;
		}

		void Window_UserResized(object sender, EventArgs e)
		{
			if (ignoreSizeChanges || !EnsureDevice() || (!IsWindowed))
				return;

			Rectangle rect = NativeMethods.GetClientRectangle(game.Window.Handle);
			if (rect.Width != CurrentSettings.BackBufferWidth || rect.Height != CurrentSettings.BackBufferHeight)
			{
				DeviceSettings newSettings = CurrentSettings.Clone();
				newSettings.BackBufferWidth = rect.Width;
				newSettings.BackBufferHeight = rect.Height;
				CreateDevice(newSettings);
			}
		}

		void Window_ScreenChanged(object sender, EventArgs e)
		{
			if (!EnsureDevice() || !CurrentSettings.Windowed || ignoreSizeChanges)
				return;

			Rectangle screenRect = NativeMethods.GetClientRectangle(game.Window.Handle);
			DeviceSettings newSettings = CurrentSettings.Clone();
			newSettings.BackBufferWidth = screenRect.Width;
			newSettings.BackBufferHeight = screenRect.Height;
			CreateDevice(newSettings);
		}

		void game_FrameEnd(object sender, EventArgs e)
		{
			if (GraphicsDevice.SwapChain == null) return;

			try
			{
				uint syncInterval = CurrentSettings.EnableVSync ? 1u : 0u;
				GraphicsDevice.SwapChain.Present(syncInterval, PresentFlags.None);
			}
			catch (Exception ex)
			{
				Trace.TraceError($"Present failed: {ex.Message}");
			}
		}

		void game_FrameStart(object sender, CancelEventArgs e)
		{
			if (GraphicsDevice.Device == null)
			{
				e.Cancel = true;
				return;
			}

			if (!game.IsActive && !this.CurrentSettings.EnableVSync)
				Thread.Sleep(this.game.InactiveSleepTime.Milliseconds);
		}

		void InitializeDevice()
		{
			try
			{
				int width = CurrentSettings.BackBufferWidth;
				int height = CurrentSettings.BackBufferHeight;
				if (width <= 0) width = 1280;
				if (height <= 0) height = 720;

				// Create D3D11 device
				var featureLevels = new[] { FeatureLevel.Level_11_0, FeatureLevel.Level_10_1, FeatureLevel.Level_10_0 };
				DeviceCreationFlags flags = DeviceCreationFlags.BgraSupport;
#if DEBUG
				// Enable debug layer in debug builds if available
				if (D3D11.SdkLayersAvailable())
					flags |= DeviceCreationFlags.Debug;
#endif
				D3D11.D3D11CreateDevice(
					null,
					DriverType.Hardware,
					flags,
					featureLevels,
					out var device,
					out var featureLevel,
					out var context);

				GraphicsDevice.Device = device;
				GraphicsDevice.Context = context;

				Trace.TraceInformation($"D3D11 device created. Feature level: {featureLevel}");

				// Create DXGI swap chain
				using var dxgiDevice = device.QueryInterface<IDXGIDevice1>();
				using var dxgiAdapter = dxgiDevice.GetAdapter();
				using var dxgiFactory = dxgiAdapter.GetParent<IDXGIFactory2>();

				var swapChainDesc = new SwapChainDescription1
				{
					Width = (uint)width,
					Height = (uint)height,
					Format = Format.B8G8R8A8_UNorm,
					SampleDescription = new SampleDescription(1, 0),
					BufferUsage = Usage.RenderTargetOutput,
					BufferCount = 2,
					SwapEffect = SwapEffect.FlipDiscard,
					Scaling = Scaling.Stretch,
				};

				GraphicsDevice.SwapChain = dxgiFactory.CreateSwapChainForHwnd(
					device, game.Window.Handle, swapChainDesc);

				// Disable Alt+Enter fullscreen toggle (we handle it ourselves)
				dxgiFactory.MakeWindowAssociation(game.Window.Handle, WindowAssociationFlags.IgnoreAltEnter);

				// Create render target view and set viewport
				GraphicsDevice.CreateRenderTargetView();
				GraphicsDevice.SetViewport(width, height);

				// Create sprite batch
				GraphicsDevice.SpriteBatch = new SpriteBatch(device, context);

				CurrentSettings.BackBufferWidth = width;
				CurrentSettings.BackBufferHeight = height;
			}
			catch (Exception e)
			{
				throw new DeviceCreationException("Could not create D3D11 graphics device.", e);
			}

			UpdateDeviceStats();

			game.Initialize();
			game.LoadContent();
		}

		void ResizeDevice()
		{
			if (GraphicsDevice.SwapChain == null)
			{
				InitializeDevice();
				return;
			}

			int width = CurrentSettings.BackBufferWidth;
			int height = CurrentSettings.BackBufferHeight;
			if (width <= 0) width = 1280;
			if (height <= 0) height = 720;

			game.UnloadContent();

			// Release the old render target view before resizing
			GraphicsDevice.RenderTargetView?.Dispose();
			GraphicsDevice.RenderTargetView = null;

			// Resize the swap chain buffers
			GraphicsDevice.SwapChain.ResizeBuffers(
				0, (uint)width, (uint)height, Format.Unknown, SwapChainFlags.None);

			// Recreate render target view and set viewport
			GraphicsDevice.CreateRenderTargetView();
			GraphicsDevice.SetViewport(width, height);

			CurrentSettings.BackBufferWidth = width;
			CurrentSettings.BackBufferHeight = height;

			UpdateDeviceStats();
			game.LoadContent();
		}

		void ReleaseDevice()
		{
			if (GraphicsDevice.Device == null)
				return;

			if (game != null)
			{
				game.UnloadContent();
				game.Dispose(true);
			}

			GraphicsDevice.Dispose();
			GraphicsDevice.Device = null;
			GraphicsDevice.Context = null;
			GraphicsDevice.SwapChain = null;
		}

		void UpdateDeviceInformation()
		{
			DeviceInformation = "D3D11 Hardware";
		}

		void UpdateDeviceStats()
		{
			StringBuilder builder = new StringBuilder();
			builder.Append("D3D11 Vsync ");
			builder.Append(CurrentSettings.EnableVSync ? "on" : "off");
			builder.AppendFormat(" ({0}x{1}), B8G8R8A8_UNorm",
				CurrentSettings.BackBufferWidth,
				CurrentSettings.BackBufferHeight);
			DeviceStatistics = builder.ToString();
		}
	}
}
