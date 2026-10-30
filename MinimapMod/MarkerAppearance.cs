namespace MinimapMod
{
    internal static class MarkerAppearance
    {
        public static float BorderInset(float screenPixels, float pixelsPerLocalUnit, float parentSize)
        {
            if (float.IsNaN(pixelsPerLocalUnit) || float.IsInfinity(pixelsPerLocalUnit)
                || pixelsPerLocalUnit <= 0 || parentSize <= 0) return 0;
            // Preserve at least half the portrait diameter at extreme zoom-out.
            return System.Math.Min(screenPixels / pixelsPerLocalUnit, parentSize * 0.25f);
        }
    }
}
