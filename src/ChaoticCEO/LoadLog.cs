using System.IO;

namespace ChaoticCEO
{
    public static class LoadLog
    {
        public const string Version = "1.3.2";
        public const string PinnedBuild = "3682";
        public const string ModId = "ChaoticCEO";

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

        public static string RecordPinned(string localAppData)
        {
            return Record(CeoLogPath(localAppData));
        }

        public static string InstallDll(string modsLocalRoot)
        {
            return Path.Combine(modsLocalRoot, ModId, ModId + ".dll");
        }
    }
}
