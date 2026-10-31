using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class LayoutCopy
    {
        public LayoutCopy(
            string street,
            string family,
            string blueprint,
            int source,
            int destination,
            bool copied,
            string call)
        {
            Street = street ?? "";
            Family = family ?? "";
            Blueprint = blueprint ?? "";
            Source = source;
            Destination = destination;
            Copied = copied;
            Call = call ?? "";
        }

        public string Street { get; }

        public string Family { get; }

        public string Blueprint { get; }

        public int Source { get; }

        public int Destination { get; }

        public bool Copied { get; }

        public string Call { get; }
    }

    public static class Clone
    {
        public static LayoutCopy Confirm(
            ApiNote? api,
            LayoutClone? layout,
            string? street,
            string? blueprint,
            bool confirmed)
        {
            if (!confirmed
                || api == null
                || api.Clone.Length == 0
                || layout == null
                || string.IsNullOrEmpty(street)
                || string.IsNullOrEmpty(blueprint)
                || layout.Street != street
                || layout.Blueprint != blueprint
                || layout.Family.Length == 0
                || layout.Blueprint.Length == 0
                || layout.Source <= 0
                || layout.Destination <= 0)
            {
                return None();
            }

            return new LayoutCopy(
                layout.Street,
                layout.Family,
                layout.Blueprint,
                layout.Source,
                layout.Destination,
                true,
                api.Clone);
        }

        public static string Record(string localAppData, LayoutCopy? copy)
        {
            if (copy == null || !copy.Copied)
            {
                return "";
            }

            var line = "layout.cloned version=" + LoadLog.Version
                + " street=" + copy.Street
                + " family=" + copy.Family
                + " blueprint=" + copy.Blueprint
                + " source=" + copy.Source.ToString(CultureInfo.InvariantCulture)
                + " destination=" + copy.Destination.ToString(CultureInfo.InvariantCulture)
                + " call=" + copy.Call;
            var path = LoadLog.CeoLogPath(localAppData);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + System.Environment.NewLine);
            return line;
        }

        public static LayoutCopy Reload(LayoutCopy? copy)
        {
            return None();
        }

        private static LayoutCopy None()
        {
            return new LayoutCopy("", "", "", 0, 0, false, "");
        }
    }
}
