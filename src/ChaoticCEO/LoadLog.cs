using System;
using System.Collections.Generic;
using System.IO;

namespace ChaoticCEO
{
    public static class LoadLog
    {
        public const string Version = "1.17.9.9";
        public const string PinnedBuild = "3682";
        public const string ModId = "ChaoticCEO";
        public const int HistoryLimit = 200;

        public static string Line(string build)
        {
            return "mod.loaded version=" + Version + " build=" + build;
        }

        public static string Record(string path)
        {
            return Record(path, PinnedBuild);
        }

        public static string Record(string path, string build)
        {
            var line = Line(build);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + System.Environment.NewLine);
            return line;
        }

        public static string CeoLogPath(string localAppData)
        {
            return Path.Combine(localAppData, ModId, "ceo.log");
        }

        public static string HistoryPath(string localAppData)
        {
            return Path.Combine(localAppData, ModId, "history.log");
        }

        public static string RecordPinned(string localAppData)
        {
            return Record(CeoLogPath(localAppData));
        }

        public static string StartSession(string localAppData)
        {
            var current = CeoLogPath(localAppData);
            RememberFile(current, HistoryPath(localAppData));
            var line = Line(PinnedBuild);
            var directory = Path.GetDirectoryName(current);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(current, line + Environment.NewLine);
            return line;
        }

        public static string Remember(string localAppData, string shown)
        {
            if (shown.Length == 0 || shown.IndexOf(" steps=", StringComparison.Ordinal) < 0)
            {
                return "";
            }

            var history = HistoryPath(localAppData);
            var kept = ReadLines(history);
            if (SameRoadmap(LastRoadmap(kept), shown))
            {
                return "";
            }

            var changed = "roadmap.changed" + shown.Substring("roadmap.shown".Length);
            kept.Add(changed);
            WriteTrimmed(history, kept);
            return changed;
        }

        public static void RememberFile(string currentPath, string historyPath)
        {
            var kept = ReadLines(historyPath);
            foreach (var raw in ReadLines(currentPath))
            {
                if (raw.StartsWith("store.closed ", StringComparison.Ordinal)
                    || raw.StartsWith("logistics.redone ", StringComparison.Ordinal)
                    || raw.StartsWith("price.applied ", StringComparison.Ordinal)
                    || raw.StartsWith("staff.applied ", StringComparison.Ordinal)
                    || raw.StartsWith("building.offered ", StringComparison.Ordinal)
                    || raw.StartsWith("building.rented ", StringComparison.Ordinal)
                    || raw.StartsWith("layout.loaded ", StringComparison.Ordinal)
                    || raw.StartsWith("layout.cloned ", StringComparison.Ordinal)
                    || raw.StartsWith("ops.assigned ", StringComparison.Ordinal)
                    || raw.StartsWith("capacity.matched ", StringComparison.Ordinal)
                    || raw.StartsWith("contract.signed ", StringComparison.Ordinal))
                {
                    kept.Add(raw);
                    continue;
                }

                if (!raw.StartsWith("roadmap.shown ", StringComparison.Ordinal)
                    && !raw.StartsWith("roadmap.changed ", StringComparison.Ordinal))
                {
                    continue;
                }

                if (SameRoadmap(LastRoadmap(kept), raw))
                {
                    continue;
                }

                kept.Add(raw.StartsWith("roadmap.shown ", StringComparison.Ordinal)
                    ? "roadmap.changed" + raw.Substring("roadmap.shown".Length)
                    : raw);
            }

            if (kept.Count == 0 && !File.Exists(historyPath))
            {
                return;
            }

            WriteTrimmed(historyPath, kept);
        }

        private static List<string> ReadLines(string path)
        {
            var lines = new List<string>();
            if (!File.Exists(path))
            {
                return lines;
            }

            foreach (var raw in File.ReadAllLines(path))
            {
                if (raw.Length > 0)
                {
                    lines.Add(raw);
                }
            }

            return lines;
        }

        private static void WriteTrimmed(string path, List<string> lines)
        {
            if (lines.Count > HistoryLimit)
            {
                lines.RemoveRange(0, lines.Count - HistoryLimit);
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, string.Join(Environment.NewLine, lines) + Environment.NewLine);
        }

        private static string LastRoadmap(List<string> lines)
        {
            for (var index = lines.Count - 1; index >= 0; index--)
            {
                var line = lines[index];
                if (line.StartsWith("roadmap.changed ", StringComparison.Ordinal)
                    || line.StartsWith("roadmap.shown ", StringComparison.Ordinal))
                {
                    return line;
                }
            }

            return "";
        }

        private static bool SameRoadmap(string earlier, string later)
        {
            var left = RoadmapKey(earlier);
            var right = RoadmapKey(later);
            return left.Length > 0 && left == right;
        }

        private static string RoadmapKey(string line)
        {
            var steps = line.IndexOf(" steps=", StringComparison.Ordinal);
            if (steps < 0)
            {
                return "";
            }

            return line.Substring(steps);
        }

        public static string InstallDll(string modsLocalRoot)
        {
            return Path.Combine(modsLocalRoot, ModId, ModId + ".dll");
        }
    }
}
