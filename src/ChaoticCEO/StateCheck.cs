using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ChaoticCEO
{
    public static class StateCheck
    {
        public static string Line(int day, string where, string street, int number, string? expected, string? actual)
        {
            if (Same(expected, actual))
            {
                return "";
            }

            return "SILENT_NOOP version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " where=" + Token(where)
                + " expected=" + Token(expected)
                + " actual=" + Token(actual)
                + " street=" + Token(street)
                + " number=" + number.ToString(CultureInfo.InvariantCulture);
        }

        public static string Record(
            string? localAppData,
            int day,
            string where,
            string street,
            int number,
            string? expected,
            string? actual)
        {
            var line = Line(day, where, street, number, expected, actual);
            if (line.Length == 0 || string.IsNullOrEmpty(localAppData))
            {
                return line;
            }

            var path = LoadLog.CeoLogPath(localAppData);
            if (File.Exists(path))
            {
                foreach (var existing in File.ReadAllLines(path))
                {
                    if (existing == line)
                    {
                        CrashTrace.Note("state." + Token(where), "SILENT_NOOP");
                        return line;
                    }
                }
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + Environment.NewLine);
            CrashTrace.Note("state." + Token(where), "SILENT_NOOP");
            return line;
        }

        private static bool Same(string? expected, string? actual)
        {
            var want = Reading(expected);
            if (want.Length == 0)
            {
                return true;
            }

            return want == Reading(actual);
        }

        private static string Reading(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "";
            }

            var text = value.Trim();
            if (text == "ba:businesstype_empty")
            {
                return "";
            }

            return text;
        }

        private static string Token(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            var text = new StringBuilder(value.Length);
            var spaced = false;
            foreach (var letter in value)
            {
                if (letter == ' ' || letter == '\t' || letter == '\r' || letter == '\n' || letter == '=')
                {
                    if (!spaced && text.Length > 0)
                    {
                        text.Append('_');
                    }

                    spaced = true;
                    continue;
                }

                spaced = false;
                text.Append(letter);
            }

            return text.ToString();
        }
    }
}
