using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class AdviceNote
    {
        public AdviceNote(string tone, string business, string street, int number, string explain)
        {
            Tone = tone ?? "";
            Business = business ?? "";
            Street = street ?? "";
            Number = number;
            Explain = explain ?? "";
        }

        public string Tone { get; }

        public string Business { get; }

        public string Street { get; }

        public int Number { get; }

        public string Explain { get; }
    }

    public static class Advice
    {
        public static IReadOnlyList<AdviceNote> Explain(IReadOnlyList<OverlayRow> rows)
        {
            var notes = new List<AdviceNote>();
            if (rows == null)
            {
                return notes;
            }

            foreach (var row in rows)
            {
                if (row == null)
                {
                    continue;
                }

                notes.Add(new AdviceNote(row.Tone, row.Business, row.Street, row.Number, OneLine(row)));
            }

            return notes;
        }

        public static IReadOnlyList<string> Record(string localAppData, IReadOnlyList<OverlayRow> rows)
        {
            var notes = Explain(rows);
            var written = new List<string>(notes.Count);
            if (notes.Count == 0)
            {
                return written;
            }

            var path = LoadLog.CeoLogPath(localAppData);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            foreach (var note in notes)
            {
                var line = Shown(note);
                File.AppendAllText(path, line + Environment.NewLine);
                written.Add(line);
            }

            return written;
        }

        public static string Shown(AdviceNote note)
        {
            return "advice.shown version=" + LoadLog.Version
                + " tone=" + note.Tone
                + " business=" + note.Business
                + " street=" + note.Street
                + " number=" + note.Number.ToString(CultureInfo.InvariantCulture)
                + " explain=" + note.Explain;
        }

        private static string OneLine(OverlayRow row)
        {
            string text;
            if (row.Tone == "warning")
            {
                text = row.Business + " mixes " + row.Detail + ".";
            }
            else if (row.Tone == "open")
            {
                text = "Open " + row.Detail + " on " + row.Street + ". "
                    + row.Number.ToString(CultureInfo.InvariantCulture) + " already.";
            }
            else
            {
                text = "Close " + row.Business + ". Profit " + FormatProfit(row.Profit) + ".";
            }

            return text.Replace("\r", " ").Replace("\n", " ");
        }

        private static string FormatProfit(double value)
        {
            var rounded = Math.Round(value, 4, MidpointRounding.ToEven);
            if (rounded == Math.Truncate(rounded))
            {
                return ((long)rounded).ToString(CultureInfo.InvariantCulture);
            }

            return rounded.ToString("0.####", CultureInfo.InvariantCulture);
        }
    }
}
