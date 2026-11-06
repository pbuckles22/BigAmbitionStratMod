using System;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public static class Health
    {
        private static string _seen = "";

        public static string Section(string? summary, string? log)
        {
            var healed = Field(summary, "healed");
            var last = "healed=" + healed.ToString(CultureInfo.InvariantCulture);
            if (healed == 0)
            {
                last += " none";
            }
            else
            {
                var tail = Tail(LastHealed(log));
                last += tail.Length == 0 ? " none" : " " + tail;
            }

            return "Stores supplied unconnected=" + Field(summary, "unconnected").ToString(CultureInfo.InvariantCulture)
                + "  Slots filled emptySlots=" + Field(summary, "emptySlots").ToString(CultureInfo.InvariantCulture)
                + "  Plans present missingPlans=" + Field(summary, "missingPlans").ToString(CultureInfo.InvariantCulture)
                + "  Last heal " + last;
        }

        public static string Latest(string? localAppData)
        {
            var path = LoadLog.CeoLogPath(localAppData ?? "");
            if (!File.Exists(path))
            {
                return Section(null, null);
            }

            var text = File.ReadAllText(path);
            return Section(LastLine(text, "audit.summary "), text);
        }

        public static string Record(string localAppData, string line)
        {
            var shown = "health.shown version=" + LoadLog.Version + " " + (line ?? "");
            var key = (localAppData ?? "") + "\n" + shown;
            if (key == _seen)
            {
                return shown;
            }

            _seen = key;
            var path = LoadLog.CeoLogPath(localAppData ?? "");
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, shown + Environment.NewLine);
            return shown;
        }

        private static int Field(string? line, string name)
        {
            if (string.IsNullOrEmpty(line))
            {
                return 0;
            }

            var token = name + "=";
            var at = line.IndexOf(token, StringComparison.Ordinal);
            if (at < 0)
            {
                return 0;
            }

            var rest = line.Substring(at + token.Length);
            var end = 0;
            while (end < rest.Length && char.IsDigit(rest[end]))
            {
                end++;
            }

            if (end == 0)
            {
                return 0;
            }

            return int.Parse(rest.Substring(0, end), CultureInfo.InvariantCulture);
        }

        private static string? LastHealed(string? log)
        {
            if (string.IsNullOrEmpty(log))
            {
                return null;
            }

            string? found = null;
            var lines = log.Split('\n');
            foreach (var raw in lines)
            {
                var line = raw.Trim().TrimEnd('\r');
                if (line.StartsWith("audit.healed ", StringComparison.Ordinal))
                {
                    found = line;
                }
            }

            return found;
        }

        private static string? LastLine(string text, string prefix)
        {
            string? found = null;
            var lines = text.Split('\n');
            foreach (var raw in lines)
            {
                var line = raw.Trim().TrimEnd('\r');
                if (line.StartsWith(prefix, StringComparison.Ordinal))
                {
                    found = line;
                }
            }

            return found;
        }

        private static string Tail(string? healed)
        {
            if (string.IsNullOrEmpty(healed))
            {
                return "";
            }

            var at = healed.IndexOf("action=", StringComparison.Ordinal);
            if (at < 0)
            {
                return "";
            }

            var tail = healed.Substring(at);
            var dry = tail.LastIndexOf(" dry=", StringComparison.Ordinal);
            if (dry >= 0)
            {
                tail = tail.Substring(0, dry);
            }

            var call = tail.LastIndexOf(" call=", StringComparison.Ordinal);
            if (call >= 0)
            {
                tail = tail.Substring(0, call);
            }

            return tail.Trim();
        }
    }
}
