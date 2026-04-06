using System;

namespace SampleFramework
{
    /// <summary>
    /// Contains settings for creating a 3D device.
    /// </summary>
    public class DeviceSettings : ICloneable
    {
        public int AdapterOrdinal { get; set; }
        public int RefreshRate { get; set; }
        public int BackBufferWidth { get; set; }
        public int BackBufferHeight { get; set; }
        public int BackBufferCount { get; set; }
        public bool Windowed { get; set; }
        public bool EnableVSync { get; set; }
        public bool Multithreaded { get; set; }
        public int MultisampleQuality { get; set; }

        public DeviceSettings()
        {
            BackBufferCount = 2;
            Windowed = true;
            EnableVSync = true;
        }

        public DeviceSettings Clone()
        {
            DeviceSettings result = new DeviceSettings();
            result.AdapterOrdinal = AdapterOrdinal;
            result.RefreshRate = RefreshRate;
            result.BackBufferWidth = BackBufferWidth;
            result.BackBufferHeight = BackBufferHeight;
            result.BackBufferCount = BackBufferCount;
            result.Windowed = Windowed;
            result.EnableVSync = EnableVSync;
            result.Multithreaded = Multithreaded;
            result.MultisampleQuality = MultisampleQuality;
            return result;
        }

        object ICloneable.Clone()
        {
            return Clone();
        }

        /// <summary>
        /// Validates settings. With D3D11, most D3D9 enumeration is unnecessary.
        /// Returns a clone of the input settings.
        /// </summary>
        public static DeviceSettings FindValidSettings(DeviceSettings settings)
        {
            return settings.Clone();
        }
    }
}
