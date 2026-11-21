using System;

namespace ChaoticCEO
{
    public static class CourseBridge
    {
        public static string Required(int priorStores)
        {
            if (priorStores < 1)
            {
                return "";
            }

            try
            {
                if (EducationHelper.AllDiplomas == null)
                {
                    return "";
                }

                if (EducationHelper.HasCompletedDiploma(DiplomaName.MarketDemands))
                {
                    return "";
                }
            }
            catch (Exception ex)
            {
                CrashTrace.Seen(ex, "CourseBridge.Required", "MarketDemands");
                return "";
            }

            return "Market Demands";
        }
    }
}
