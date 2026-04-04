using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace FDK
{
	public class CFPS
	{
		// プロパティ

		public int n現在のFPS
		{
			get;
			private set;
		}
		public bool bFPSの値が変化した
		{
			get;
			private set;
		}

		/// <summary>Average frame time in ms over the last reporting interval.</summary>
		public double dbCurrentFrameTimeMs { get; private set; }

		/// <summary>Maximum frame time in ms over the last reporting interval.</summary>
		public double dbMaxFrameTimeMs { get; private set; }

		/// <summary>Minimum frame time in ms over the last reporting interval.</summary>
		public double dbMinFrameTimeMs { get; private set; }


		// コンストラクタ

		public CFPS()
		{
			this.n現在のFPS = 0;
			this.timer = new CTimer( CTimer.EType.MultiMedia );
			this.基点時刻ms = this.timer.nCurrentTime;
			this.内部FPS = 0;
			this.bFPSの値が変化した = false;
			this.swFrameTime = Stopwatch.StartNew();
			this.dbAccumulatedFrameTimeMs = 0;
			this.dbIntervalMaxMs = 0;
			this.dbIntervalMinMs = double.MaxValue;
			this.nFrameTimeSamples = 0;
		}


		// メソッド

		public void tカウンタ更新()
		{
			// Track per-frame time using high-resolution stopwatch
			double elapsed = this.swFrameTime.Elapsed.TotalMilliseconds;
			this.swFrameTime.Restart();
			this.dbAccumulatedFrameTimeMs += elapsed;
			this.nFrameTimeSamples++;
			if (elapsed > this.dbIntervalMaxMs) this.dbIntervalMaxMs = elapsed;
			if (elapsed < this.dbIntervalMinMs) this.dbIntervalMinMs = elapsed;

			this.timer.tUpdate();
			this.bFPSの値が変化した = false;

			const long INTERVAL = 1000;
			while( ( this.timer.nCurrentTime - this.基点時刻ms ) >= INTERVAL )
			{
				this.n現在のFPS = this.内部FPS;
				this.内部FPS = 0;
				this.bFPSの値が変化した = true;
				this.基点時刻ms += INTERVAL;

				// Compute frame time stats for the interval
				if (this.nFrameTimeSamples > 0)
				{
					this.dbCurrentFrameTimeMs = this.dbAccumulatedFrameTimeMs / this.nFrameTimeSamples;
					this.dbMaxFrameTimeMs = this.dbIntervalMaxMs;
					this.dbMinFrameTimeMs = this.dbIntervalMinMs;
				}
				this.dbAccumulatedFrameTimeMs = 0;
				this.dbIntervalMaxMs = 0;
				this.dbIntervalMinMs = double.MaxValue;
				this.nFrameTimeSamples = 0;
			}
			this.内部FPS++;
		}


		// その他

		#region [ private ]
		//-----------------
		private CTimer	timer;
		private long	基点時刻ms;
		private int		内部FPS;
		private Stopwatch swFrameTime;
		private double	dbAccumulatedFrameTimeMs;
		private double	dbIntervalMaxMs;
		private double	dbIntervalMinMs;
		private int		nFrameTimeSamples;
		//-----------------
		#endregion
	}
}
