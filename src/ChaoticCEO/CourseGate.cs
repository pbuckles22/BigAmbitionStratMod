using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ChaoticCEO
{
    public static class CourseGate
    {
        public const string ExpansionSlug = "expansion-blocked";

        public static string Name(
            IReadOnlyList<StoreHealth>? priorStores,
            string? place,
            FitOutProgress? progress,
            string? requiredCourse)
        {
            if (priorStores == null || priorStores.Count == 0)
            {
                return "";
            }

            if (string.IsNullOrEmpty(place) || progress == null || progress.LayoutLoaded)
            {
                return "";
            }

            var course = Slug(requiredCourse);
            if (course.Length > 0)
            {
                return course;
            }

            if (!StoreHealthGate.ExpansionAllowed(priorStores))
            {
                return ExpansionSlug;
            }

            return "";
        }

        public static string Slug(string? course)
        {
            if (string.IsNullOrWhiteSpace(course))
            {
                return "";
            }

            var text = course.Trim();
            if (text.StartsWith("ba:", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(3);
            }

            const string diploma = "diplomaname_";
            if (text.StartsWith(diploma, StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(diploma.Length);
            }

            var slug = new StringBuilder(text.Length + 4);
            var hyphen = false;
            var letter = false;
            foreach (var item in text)
            {
                if (item == ' ' || item == '_' || item == '-' || item == '/')
                {
                    if (letter && !hyphen)
                    {
                        slug.Append('-');
                        hyphen = true;
                    }

                    continue;
                }

                if (!char.IsLetterOrDigit(item))
                {
                    continue;
                }

                if (char.IsUpper(item) && letter && !hyphen)
                {
                    slug.Append('-');
                }

                slug.Append(char.ToLowerInvariant(item));
                letter = true;
                hyphen = false;
            }

            while (slug.Length > 0 && slug[slug.Length - 1] == '-')
            {
                slug.Length--;
            }

            if (slug.ToString() == "marketdemands")
            {
                return "market-demands";
            }

            return slug.ToString();
        }

        public static string Line(string? gate, int day, string street, int number)
        {
            if (string.IsNullOrEmpty(gate))
            {
                return "";
            }

            if (gate == ExpansionSlug)
            {
                return NamedBlock.Line(day, "fitout", gate, street, number);
            }

            return "course.needed version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " course=" + gate
                + " street=" + (street ?? "")
                + " number=" + number.ToString(CultureInfo.InvariantCulture);
        }

        public static string Record(string? localAppData, string? gate, int day, string street, int number)
        {
            var line = Line(gate, day, street, number);
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
            var where = gate == ExpansionSlug ? "block.fitout" : "course.needed";
            CrashTrace.Note(where, gate ?? "");
            return line;
        }
    }
}
