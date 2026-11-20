namespace Nightmare.Core.World
{
    /// <summary>Computes authored candle drain and visible candle counts independently of Unity objects.</summary>
    public static class CaptureRules
    {
        /// <summary>Returns the authored candle drain rate for the number of occupying enemies.</summary>
        public static float DamagePerSecond(int enemies) => enemies <= 0 ? 0f : enemies >= 4 ? 7.5f : enemies >= 2 ? 5f : 2.5f;
        /// <summary>Converts percentage progress to a truncated candle count, clamped to the authored count.</summary>
        public static int LitCandles(float progress, int count)
        {
            int lit = (int)(progress / 100f * count);
            return lit < 0 ? 0 : lit > count ? count : lit;
        }
    }
}
