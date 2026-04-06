using FDK;

namespace DTXMania
{
	/// <summary>
	/// Legacy GPU flush activity. Under D3D11 flip model, explicit flushing
	/// is unnecessary and can cause stalls. Kept as a no-op for config compatibility.
	/// </summary>
	internal class CActFlushGPU : CActivity
	{
		public override int OnUpdateAndDraw()
		{
			return 0;
		}
	}
}
