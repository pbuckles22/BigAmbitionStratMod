using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public static class Saga
    {
        public const string Lease = "lease";

        public const string FitOut = "fit-out";

        public const string Staff = "staff";

        public const string Stock = "stock";

        public const string Open = "open";

        private static readonly object Gate = new object();

        private static readonly Dictionary<string, string> First = new Dictionary<string, string>(StringComparer.Ordinal);

        private static int _seenDay;

        private static bool _readDay;

        public static void Reset()
        {
            lock (Gate)
            {
                First.Clear();
                _seenDay = 0;
                _readDay = false;
            }
        }

        public static string Id(string? street, int number)
        {
            var name = (street ?? "").Trim();
            if (name.Length == 0 || number <= 0)
            {
                return "";
            }

            return name + "-" + number.ToString(CultureInfo.InvariantCulture);
        }

        public static string Step(string? stage, int day, string? street, int number)
        {
            var name = StageName(stage);
            var id = Id(street, number);
            if (name.Length == 0 || id.Length == 0)
            {
                return "";
            }

            return "saga version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " stage=" + name
                + " saga_id=" + id
                + " street=" + (street ?? "").Trim()
                + " number=" + number.ToString(CultureInfo.InvariantCulture);
        }

        public static string Fork(int fromDay, int toDay)
        {
            if (fromDay <= 0 || toDay <= 0 || toDay >= fromDay)
            {
                return "";
            }

            return "TIMELINE_FORK version=" + LoadLog.Version
                + " fromDay=" + fromDay.ToString(CultureInfo.InvariantCulture)
                + " toDay=" + toDay.ToString(CultureInfo.InvariantCulture);
        }

        public static string Note(int day)
        {
            lock (Gate)
            {
                return Advance(day);
            }
        }

        public static string Load(string? localAppData, int day)
        {
            string line;
            lock (Gate)
            {
                if (!_readDay)
                {
                    _seenDay = ReadDay(localAppData);
                    _readDay = true;
                }

                var before = _seenDay;
                line = Advance(day);
                if (day > 0 && _seenDay != before)
                {
                    WriteDay(localAppData, _seenDay);
                }
            }

            if (line.Length == 0 || string.IsNullOrEmpty(localAppData))
            {
                return line;
            }

            Append(localAppData, line);
            return line;
        }

        public static string Record(string? localAppData, string? stage, int day, string? street, int number)
        {
            var line = Step(stage, day, street, number);
            if (line.Length == 0)
            {
                return "";
            }

            var key = StageName(stage) + "\n" + Id(street, number);
            lock (Gate)
            {
                if (First.TryGetValue(key, out var existing))
                {
                    return existing;
                }

                First[key] = line;
            }

            if (!string.IsNullOrEmpty(localAppData))
            {
                Append(localAppData, line);
            }

            return line;
        }

        public static string Follow(string? localAppData, string? leaseLine, string? fitLine)
        {
            After(localAppData, leaseLine, DayOf(leaseLine));
            After(localAppData, fitLine, DayOf(fitLine));
            var street = Field(leaseLine, "street");
            var number = Int(Field(leaseLine, "number"));
            var day = DayOf(fitLine);
            if (day <= 0)
            {
                day = DayOf(leaseLine);
            }

            return Step(FitOut, day, street, number);
        }

        public static string After(string? localAppData, string? line, int day)
        {
            if (string.IsNullOrEmpty(line))
            {
                return "";
            }

            if (line.StartsWith("building.rented ", StringComparison.Ordinal))
            {
                Record(localAppData, Lease, DayOf(line), Field(line, "street"), Int(Field(line, "number")));
                return line;
            }

            if (line.StartsWith("layout.loaded ", StringComparison.Ordinal))
            {
                Record(localAppData, FitOut, DayOf(line), Field(line, "street"), Int(Field(line, "number")));
                return line;
            }

            if (line.StartsWith("ops.assigned ", StringComparison.Ordinal))
            {
                Record(localAppData, Staff, DayOf(line), Field(line, "street"), Int(Field(line, "number")));
                return line;
            }

            if (line.StartsWith("contract.signed ", StringComparison.Ordinal))
            {
                Record(localAppData, Stock, DayOf(line), Field(line, "street"), Int(Field(line, "number")));
                return line;
            }

            if (line.StartsWith("store.health ", StringComparison.Ordinal)
                && line.IndexOf("g1=true", StringComparison.Ordinal) >= 0)
            {
                var place = Between(line, "place=", " g1=");
                var split = place.LastIndexOf(' ');
                if (split > 0)
                {
                    var street = place.Substring(0, split);
                    var number = Int(place.Substring(split + 1));
                    var healthDay = day > 0 ? day : DayOf(line);
                    Record(localAppData, Open, healthDay, street, number);
                }

                return line;
            }

            return line;
        }

        private static string Advance(int day)
        {
            if (day <= 0)
            {
                return "";
            }

            var line = Fork(_seenDay, day);
            if (day > _seenDay || line.Length > 0)
            {
                _seenDay = day;
            }

            return line;
        }

        private static string StageName(string? stage)
        {
            var name = (stage ?? "").Trim();
            if (name == Lease || name == FitOut || name == Staff || name == Stock || name == Open)
            {
                return name;
            }

            return "";
        }

        private static int ReadDay(string? localAppData)
        {
            if (string.IsNullOrEmpty(localAppData))
            {
                return 0;
            }

            var path = TimelinePath(localAppData);
            if (!File.Exists(path))
            {
                return 0;
            }

            return Int(File.ReadAllText(path).Trim());
        }

        private static void WriteDay(string? localAppData, int day)
        {
            if (string.IsNullOrEmpty(localAppData) || day <= 0)
            {
                return;
            }

            var path = TimelinePath(localAppData);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, day.ToString(CultureInfo.InvariantCulture));
        }

        private static string TimelinePath(string localAppData)
        {
            return Path.Combine(localAppData, LoadLog.ModId, "timeline.day");
        }

        private static void Append(string localAppData, string line)
        {
            var path = LoadLog.CeoLogPath(localAppData);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + Environment.NewLine);
        }

        private static int DayOf(string? line)
        {
            return Int(Field(line, "day"));
        }

        private static int Int(string text)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : 0;
        }

        private static string Field(string? line, string name)
        {
            if (string.IsNullOrEmpty(line))
            {
                return "";
            }

            var token = name + "=";
            var start = line.IndexOf(token, StringComparison.Ordinal);
            if (start < 0)
            {
                return "";
            }

            start += token.Length;
            var end = line.IndexOf(' ', start);
            if (end < 0)
            {
                end = line.Length;
            }

            return line.Substring(start, end - start);
        }

        private static string Between(string line, string startToken, string endToken)
        {
            var start = line.IndexOf(startToken, StringComparison.Ordinal);
            if (start < 0)
            {
                return "";
            }

            start += startToken.Length;
            var end = line.IndexOf(endToken, start, StringComparison.Ordinal);
            if (end < 0)
            {
                end = line.Length;
            }

            return line.Substring(start, end - start);
        }
    }
}
