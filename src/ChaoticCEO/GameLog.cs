using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;

namespace ChaoticCEO
{
    public static class GameLog
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, string> Bound =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConditionalWeakTable<object, string> Ids =
            new ConditionalWeakTable<object, string>();
        private static int _nextId = 1;

        public static string Key(object? save)
        {
            if (save == null)
            {
                return "";
            }

            lock (Gate)
            {
                return Ids.GetValue(
                    save,
                    _ => "g" + (_nextId++).ToString(CultureInfo.InvariantCulture));
            }
        }

        public static string ArchivePath(string localAppData, int index)
        {
            return Path.Combine(
                localAppData ?? "",
                LoadLog.ModId,
                "games",
                index.ToString(CultureInfo.InvariantCulture),
                "ceo.log");
        }

        public static string Bind(string localAppData, string key)
        {
            if (string.IsNullOrEmpty(localAppData) || string.IsNullOrEmpty(key))
            {
                return "";
            }

            var root = Path.GetFullPath(localAppData);
            lock (Gate)
            {
                Bound.TryGetValue(root, out var held);
                if (string.Equals(held, key, StringComparison.Ordinal))
                {
                    return "";
                }

                var line = "game.opened version=" + LoadLog.Version + " key=" + key;
                if (!string.IsNullOrEmpty(held))
                {
                    Keep(localAppData);
                    WriteFresh(localAppData, line);
                    CeoLedger.Clear();
                }
                else
                {
                    AppendOpened(localAppData, line);
                }

                Bound[root] = key;
                return line;
            }
        }

        public static string Keep(string localAppData)
        {
            var current = LoadLog.CeoLogPath(localAppData ?? "");
            if (!File.Exists(current))
            {
                return "";
            }

            var text = File.ReadAllText(current);
            if (!HasGame(text))
            {
                return "";
            }

            var archive = ArchivePath(localAppData ?? "", NextIndex(localAppData ?? ""));
            var directory = Path.GetDirectoryName(archive);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(archive, text);
            return archive;
        }

        public static void Release(string localAppData)
        {
            if (string.IsNullOrEmpty(localAppData))
            {
                return;
            }

            lock (Gate)
            {
                Bound.Remove(Path.GetFullPath(localAppData));
            }
        }

        private static void WriteFresh(string localAppData, string opened)
        {
            var path = LoadLog.CeoLogPath(localAppData);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(
                path,
                LoadLog.Line(LoadLog.PinnedBuild) + Environment.NewLine + opened + Environment.NewLine);
        }

        private static void AppendOpened(string localAppData, string opened)
        {
            var path = LoadLog.CeoLogPath(localAppData);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, opened + Environment.NewLine);
        }

        private static bool HasGame(string text)
        {
            var at = 0;
            while (at < text.Length)
            {
                var end = text.IndexOf('\n', at);
                var line = end < 0 ? text.Substring(at) : text.Substring(at, end - at);
                line = line.TrimEnd('\r');
                if (line.Length > 0 && !line.StartsWith("mod.loaded ", StringComparison.Ordinal))
                {
                    return true;
                }

                if (end < 0)
                {
                    break;
                }

                at = end + 1;
            }

            return false;
        }

        private static int NextIndex(string localAppData)
        {
            var root = Path.Combine(localAppData, LoadLog.ModId, "games");
            if (!Directory.Exists(root))
            {
                return 1;
            }

            var next = 1;
            foreach (var dir in Directory.GetDirectories(root))
            {
                if (int.TryParse(
                        Path.GetFileName(dir),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var index)
                    && index >= next)
                {
                    next = index + 1;
                }
            }

            return next;
        }
    }
}
