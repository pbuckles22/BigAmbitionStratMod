using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class CourseEnrollment
    {
        public CourseEnrollment(string course, bool enroll, string stillRequired, string line)
        {
            Course = course ?? "";
            Enroll = enroll;
            StillRequired = stillRequired ?? "";
            Line = line ?? "";
        }

        public string Course { get; }

        public bool Enroll { get; }

        public string StillRequired { get; }

        public string Line { get; }

        public static CourseEnrollment None()
        {
            return new CourseEnrollment("", false, "", "");
        }
    }

    public static class CourseTake
    {
        public const string Call = "EducationHelper.Command_CompleteCourse";

        public static string Diploma(string? slug)
        {
            switch (slug)
            {
                case "market-demands":
                    return "MarketDemands";
                case "food-safety-course":
                    return "FoodSafetyCourse";
                case "auto-mechanic-education":
                    return "AutoMechanicEducation";
                case "basic-hr":
                    return "BasicHr";
                case "headquarters":
                    return "Headquarters";
                case "office-businesses":
                    return "OfficeBusinesses";
                case "product-manufacturing":
                    return "ProductManufacturing";
                default:
                    return "";
            }
        }

        public static CourseEnrollment Enroll(string? gate, int day, string street, int number)
        {
            var diploma = Diploma(gate);
            if (diploma.Length == 0)
            {
                return CourseEnrollment.None();
            }

            return new CourseEnrollment(gate ?? "", true, "", Line(gate, day, street, number));
        }

        public static string Line(string? course, int day, string street, int number)
        {
            if (string.IsNullOrEmpty(course))
            {
                return "";
            }

            return "course.taken version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " course=" + course
                + " street=" + (street ?? "")
                + " number=" + number.ToString(CultureInfo.InvariantCulture)
                + " call=" + Call;
        }

        public static string Record(string? localAppData, CourseEnrollment? enrolled)
        {
            if (enrolled == null || !enrolled.Enroll || enrolled.Line.Length == 0)
            {
                return enrolled == null ? "" : enrolled.Line;
            }

            if (string.IsNullOrEmpty(localAppData))
            {
                return enrolled.Line;
            }

            var path = LoadLog.CeoLogPath(localAppData);
            if (File.Exists(path))
            {
                foreach (var existing in File.ReadAllLines(path))
                {
                    if (existing == enrolled.Line)
                    {
                        return enrolled.Line;
                    }
                }
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, enrolled.Line + System.Environment.NewLine);
            CrashTrace.Note("course.taken", enrolled.Course);
            return enrolled.Line;
        }
    }
}
