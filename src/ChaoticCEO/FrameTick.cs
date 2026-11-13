namespace ChaoticCEO
{
    public static class FrameTick
    {
        public const float Interval = 0.5f;

        public static bool Due(float now, float nextAt)
        {
            return now >= nextAt;
        }

        public static float Next(float now)
        {
            return now + Interval;
        }
    }
}
