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

        public static bool Complete(string? slug)
        {
            var name = CourseTake.Diploma(slug);
            if (name.Length == 0)
            {
                return false;
            }

            try
            {
                var diploma = (DiplomaName)System.Enum.Parse(typeof(DiplomaName), name);
                EducationHelper.Command_CompleteCourse(diploma, true);
                return EducationHelper.HasCompletedDiploma(diploma);
            }
            catch (System.Exception ex)
            {
                CrashTrace.Seen(ex, "CourseBridge.Complete", slug ?? "");
                return false;
            }
        }
    }
}
