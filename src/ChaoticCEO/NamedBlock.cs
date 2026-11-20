using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ChaoticCEO
{
    public static class NamedBlock
    {
        public static string Line(int day, string where, string slug, string street, int number)
        {
            return "block.named version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " where=" + Token(where)
                + " slug=" + Token(slug)
                + " street=" + Token(street)
                + " number=" + number.ToString(CultureInfo.InvariantCulture);
        }

        public static string Record(string? localAppData, int day, string where, string slug, string street, int number)
        {
            var line = Line(day, where, slug, street, number);
            if (string.IsNullOrEmpty(localAppData))
            {
                return line;
            }

            var path = LoadLog.CeoLogPath(localAppData);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + Environment.NewLine);
            CrashTrace.Note("block." + Token(where), Token(slug));
            return line;
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
