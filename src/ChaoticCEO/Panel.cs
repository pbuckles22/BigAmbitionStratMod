using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class PanelLine
    {
        public PanelLine(string tone, string business, string street, int number, string explain)
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

    public readonly struct PanelPaint
    {
        public PanelPaint(
            float red,
            float green,
            float blue,
            float alpha,
            float textRed,
            float textGreen,
            float textBlue,
            int fontSize)
        {
            Red = red;
            Green = green;
            Blue = blue;
            Alpha = alpha;
            TextRed = textRed;
            TextGreen = textGreen;
            TextBlue = textBlue;
            FontSize = fontSize;
        }

        public float Red { get; }

        public float Green { get; }

        public float Blue { get; }

        public float Alpha { get; }

        public float TextRed { get; }

        public float TextGreen { get; }

        public float TextBlue { get; }

        public int FontSize { get; }
    }

    public static class Panel
    {
        public const string Hotkey = "F9";

        public static PanelPaint Paint()
        {
            return new PanelPaint(0.08f, 0.09f, 0.11f, 0.96f, 1f, 1f, 1f, 20);
        }

        public static bool EatScroll(bool overWindow, float scroll, float legacyScroll)
        {
            return overWindow && (scroll != 0f || legacyScroll != 0f);
        }

        public static float HoldWheel(bool open, bool inside, float legacyScroll)
        {
            if (!open || !inside || legacyScroll == 0f)
            {
                return 0f;
            }

            return legacyScroll;
        }

        public static float WheelStep(float legacyScroll)
        {
            return legacyScroll * 20f;
        }

        public static IReadOnlyList<PanelLine> Rows(IReadOnlyList<OverlayRow>? overlay)
        {
            var notes = Advice.Explain(overlay ?? Array.Empty<OverlayRow>());
            var lines = new List<PanelLine>(notes.Count);
            foreach (var note in notes)
            {
                lines.Add(new PanelLine(note.Tone, note.Business, note.Street, note.Number, note.Explain));
            }

            return lines;
        }

        public static IReadOnlyList<string> Text(IReadOnlyList<PanelLine>? lines)
        {
            var text = new List<string>();
            if (lines == null)
            {
                return text;
            }

            foreach (var line in lines)
            {
                if (line == null)
                {
                    continue;
                }

                text.Add(
                    line.Tone
                    + "  "
                    + line.Business
                    + "  "
                    + line.Street
                    + " "
                    + line.Number.ToString(CultureInfo.InvariantCulture));
                text.Add(line.Explain);
            }

            return text;
        }

        public static IReadOnlyList<string> Open(string localAppData, IReadOnlyList<OverlayRow>? overlay)
        {
            var lines = Rows(overlay);
            var path = LoadLog.CeoLogPath(localAppData);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var opened = "panel.opened version=" + LoadLog.Version
                + " rows=" + lines.Count.ToString(CultureInfo.InvariantCulture);
            File.AppendAllText(path, opened + Environment.NewLine);
            var written = new List<string> { opened };
            written.AddRange(Advice.Record(localAppData, overlay ?? Array.Empty<OverlayRow>()));
            return written;
        }
    }

    public sealed class PanelSession
    {
        public bool IsOpen { get; private set; }

        public IReadOnlyList<PanelLine> Press(string localAppData, IReadOnlyList<OverlayRow>? overlay)
        {
            IsOpen = !IsOpen;
            if (!IsOpen)
            {
                return Array.Empty<PanelLine>();
            }

            Panel.Open(localAppData, overlay);
            return Panel.Rows(overlay);
        }

        public IReadOnlyList<PanelLine> Draw(IReadOnlyList<OverlayRow>? overlay)
        {
            if (!IsOpen)
            {
                return Array.Empty<PanelLine>();
            }

            return Panel.Rows(overlay);
        }

        public IReadOnlyList<PanelLine> Press(string localAppData, CityBoard? board)
        {
            IsOpen = !IsOpen;
            if (!IsOpen)
            {
                return Array.Empty<PanelLine>();
            }

            Roadshow.Open(localAppData, board);
            return Roadshow.Lines(board);
        }

        public IReadOnlyList<PanelLine> Draw(CityBoard? board)
        {
            if (!IsOpen)
            {
                return Array.Empty<PanelLine>();
            }

            return Roadshow.Lines(board);
        }

        public void Close()
        {
            IsOpen = false;
        }
    }
}
