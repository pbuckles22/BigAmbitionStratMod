namespace ChaoticCEO
{
    public static class Morning
    {
        public static int CoverDay(CleanerGap? staff, bool autopilotOn, int day, int coveredDay)
        {
            if (day <= 0 || day == coveredDay)
            {
                return 0;
            }

            if (staff != null && staff.Hire && staff.Pick.Number <= 0)
            {
                return 0;
            }

            return day;
        }
    }

    public sealed class MorningWatch
    {
        public int Day { get; private set; }

        public void Mark(int day)
        {
            Day = day;
        }
    }
}
