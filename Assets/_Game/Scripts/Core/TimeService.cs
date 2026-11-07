using System;

namespace NG.Core
{
    /// <summary>
    /// Enda källan till "nu". Alla timers lagras som absoluta UTC-tider och frågar denna klass,
    /// så att de fortsätter när appen är stängd. Servertid justerar bort enhetens klockfel/fusk.
    /// </summary>
    public static class TimeService
    {
        static TimeSpan _serverOffset = TimeSpan.Zero;

        /// <summary>Anropas när backend svarar med sin tid.</summary>
        public static void SyncWithServer(DateTime serverUtc)
        {
            _serverOffset = serverUtc - DateTime.UtcNow;
        }

        public static DateTime UtcNow => DateTime.UtcNow + _serverOffset;

        public static long UnixNow => new DateTimeOffset(UtcNow).ToUnixTimeSeconds();

        public static double Remaining(long startUnix, int durationSec) =>
            Math.Max(0, startUnix + durationSec - UnixNow);

        public static float Progress(long startUnix, int durationSec) =>
            durationSec <= 0 ? 1f : (float)Math.Min(1.0, Math.Max(0.0, (UnixNow - startUnix) / (double)durationSec));

        /// <summary>Snabbspolningspris enligt GDD: ceil(remainingMin^0.75 / 2), gratis under 3 min.</summary>
        public static int SpeedUpGemCost(double remainingSec)
        {
            if (remainingSec <= 180) return 0;
            double minutes = remainingSec / 60.0;
            return Math.Max(1, (int)Math.Ceiling(Math.Pow(minutes, 0.75) / 2.0));
        }
    }
}
