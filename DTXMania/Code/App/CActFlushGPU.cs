using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using FDK;

namespace DTXMania
{
	/// <summary>
	/// Flushes the GPU every frame to prevent rendering latency.
	/// In D3D11, uses DeviceContext.Flush() combined with DWM.Flush().
	/// </summary>
	internal class CActFlushGPU : CActivity
	{
		// CActivity 実装

		public override int OnUpdateAndDraw()
		{
			if ( !base.bNotActivated )
			{
				CDTXMania.app.GraphicsDeviceManager.Direct3D9.Context.Flush();
				DWM.Flush();
			}
			return 0;
		}
	}
}
