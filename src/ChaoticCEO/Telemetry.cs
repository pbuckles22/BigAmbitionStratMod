using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ChaoticCEO
{
    public static class Telemetry
    {
        public const string Header = "day,cash,netWorth,profit,stage,unconnected,emptySlots,dry,mixed,unstaffed,missingPlans,cashBelowReserve,pinMismatch,healed,actions";

        public static string CsvPath(string localAppData)
        {
            return Path.Combine(localAppData ?? "", LoadLog.ModId, "telemetry.csv");
        }

        public static string Record(string localAppData, string? bar, string? health, string? controls)
        {
            var day = After(bar, "Day ");
            var logPath = LoadLog.CeoLogPath(localAppData ?? "");
            var log = File.Exists(logPath) ? File.ReadAllText(logPath) : "";
            var summary = LastSummary(log, day);
            var actions = Actions(log, day);
            var row = string.Join(
                ",",
                day,
                After(bar, "Cash "),
                Net(After(bar, "Net worth ")),
                After(bar, "Profit "),
                After(bar, "Stage "),
                Count(health, "unconnected"),
                Count(health, "emptySlots"),
                Count(summary, "dry"),
                Count(summary, "mixed"),
                Count(summary, "unstaffed"),
                Count(health, "missingPlans"),
                Count(summary, "cashBelowReserve"),
                Count(summary, "pinMismatch"),
                Count(health, "healed"),
                actions);
            var path = CsvPath(localAppData ?? "");
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var lines = new List<string>();
            if (File.Exists(path))
            {
                lines.AddRange(File.ReadAllLines(path));
            }

            if (lines.Count == 0)
            {
                lines.Add(Header);
            }

            var replaced = false;
            for (var index = 1; index < lines.Count; index++)
            {
                var cells = lines[index].Split(',');
                if (cells.Length > 0 && cells[0] == day)
                {
                    lines[index] = row;
                    replaced = true;
                    break;
                }
            }

            if (!replaced)
            {
                lines.Add(row);
            }

            File.WriteAllText(path, string.Join("\n", lines) + "\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            if (replaced)
            {
                return "";
            }

            var written = "telemetry.written version=" + LoadLog.Version
                + " day=" + day
                + " rows=" + (lines.Count - 1).ToString(CultureInfo.InvariantCulture)
                + " autopilot=" + (Contains(controls, "Autopilot on") ? "on" : "off")
                + " dry=" + (Contains(controls, "Dry run on") ? "on" : "off")
                + " actions=" + actions;
            File.AppendAllText(logPath, written + Environment.NewLine);
            return written;
        }

        private static string After(string? text, string label)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }

            var at = text.IndexOf(label, StringComparison.Ordinal);
            if (at < 0)
            {
                return "";
            }

            var rest = text.Substring(at + label.Length);
            var stop = rest.IndexOf("  ", StringComparison.Ordinal);
            return (stop < 0 ? rest : rest.Substring(0, stop)).Trim();
        }

        private static string Net(string worth)
        {
            var slash = worth.IndexOf(" / ", StringComparison.Ordinal);
            return slash < 0 ? worth : worth.Substring(0, slash);
        }

        private static string Count(string? line, string name)
        {
            if (string.IsNullOrEmpty(line))
            {
                return "0";
            }

            var token = name + "=";
            var at = line.IndexOf(token, StringComparison.Ordinal);
            if (at < 0)
            {
                return "0";
            }

            var rest = line.Substring(at + token.Length);
            var end = 0;
            while (end < rest.Length && char.IsDigit(rest[end]))
            {
                end++;
            }

            return end == 0 ? "0" : rest.Substring(0, end);
        }

        private static string? LastSummary(string log, string day)
        {
            string? found = null;
            var lines = log.Split('\n');
            foreach (var raw in lines)
            {
                var line = raw.Trim().TrimEnd('\r');
                if (line.StartsWith("audit.summary ", StringComparison.Ordinal) && HasDay(line, day))
                {
                    found = line;
                }
            }

            return found;
        }

        private static string Actions(string log, string day)
        {
            var names = new List<string>();
            var lines = log.Split('\n');
            foreach (var raw in lines)
            {
                var line = raw.Trim().TrimEnd('\r');
                if (!line.StartsWith("audit.healed ", StringComparison.Ordinal) || !HasDay(line, day))
                {
                    continue;
                }

                var at = line.IndexOf("action=", StringComparison.Ordinal);
                if (at < 0)
                {
                    continue;
                }

                var rest = line.Substring(at + "action=".Length);
                var end = rest.IndexOf(' ');
                names.Add(end < 0 ? rest : rest.Substring(0, end));
            }

            return string.Join("|", names);
        }

        private static bool HasDay(string line, string day)
        {
            var token = "day=" + day;
            var at = 0;
            while (at < line.Length)
            {
                var found = line.IndexOf(token, at, StringComparison.Ordinal);
                if (found < 0)
                {
                    return false;
                }

                var after = found + token.Length;
                if (after >= line.Length || !char.IsDigit(line[after]))
                {
                    return true;
                }

                at = after;
            }

            return false;
        }

        private static bool Contains(string? text, string token)
        {
            return !string.IsNullOrEmpty(text) && text.IndexOf(token, StringComparison.Ordinal) >= 0;
        }
    }
}
