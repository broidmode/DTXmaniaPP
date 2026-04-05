using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using Vortice.Direct3D9;
using Vortice.Mathematics;
using System.Numerics;
using FDK;

namespace DTXMania
{
    internal class CActPerfDrumsFillingEffect : CActivity
    {

        public CActPerfDrumsFillingEffect()
		{
			base.bNotActivated = true;
		}

        public override void OnManagedCreateResources()
        {
            if (!base.bNotActivated)
            {
            }
        }
        public override void OnManagedReleaseResources()
        {
            if (!base.bNotActivated)
            {
                base.OnManagedReleaseResources();
            }
        }
        public override void OnDeactivate()
        {
        }
        public override int OnUpdateAndDraw()
        {
            return 0;
        }


        // Other

        #region [ private ]
        //-----------------

        //-----------------
        #endregion
    }
}
