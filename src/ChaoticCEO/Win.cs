using System;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public static class Win
    {
        private static decimal? _approved;

        public static decimal Milestone => _approved ?? TopBar.WinNetWorth;

        public static void Approve(decimal target)
        {
            _approved = target > 0m ? target : null;
        }

        public static void ClearApproved()
        {
            _approved = null;
        }

        public static bool Crossed(decimal netWorth)
        {
            return netWorth >= Milestone;
        }

        public static bool AlreadyLogged(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            return File.ReadAllText(path).IndexOf("win.reached ", StringComparison.Ordinal) >= 0;
        }

        public static string Line(int day, decimal netWorth)
        {
            return "win.reached version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " netWorth=" + netWorth.ToString(CultureInfo.InvariantCulture);
        }

        public static string Record(string? path, int day, decimal netWorth)
        {
            if (string.IsNullOrWhiteSpace(path) || day <= 0)
            {
                return "";
            }

            if (AlreadyLogged(path) || !Crossed(netWorth))
            {
                return "";
            }

            var line = Line(day, netWorth);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + Environment.NewLine);
            return line;
        }
    }
}
