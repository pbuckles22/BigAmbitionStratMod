using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class TaxPick
    {
        public TaxPick(bool paid, decimal current, decimal back, string currentCall, string backCall)
        {
            Paid = paid;
            Current = current;
            Back = back;
            CurrentCall = currentCall ?? "";
            BackCall = backCall ?? "";
        }

        public bool Paid { get; }

        public decimal Current { get; }

        public decimal Back { get; }

        public string CurrentCall { get; }

        public string BackCall { get; }
    }

    public static class TaxBill
    {
        public static TaxPick Pay(decimal currentDue, decimal backDue, ApiNote? api)
        {
            if (api == null || (currentDue <= 0m && backDue <= 0m))
            {
                return new TaxPick(false, 0m, 0m, "", "");
            }

            var currentCall = currentDue > 0m ? api.Call("taxes pay") : "";
            var backCall = backDue > 0m ? api.Call("taxes back") : "";
            if (currentDue > 0m && currentCall.Length == 0)
            {
                return new TaxPick(false, 0m, 0m, "", "");
            }

            if (backDue > 0m && backCall.Length == 0)
            {
                return new TaxPick(false, 0m, 0m, "", "");
            }

            return new TaxPick(true, currentDue > 0m ? currentDue : 0m, backDue > 0m ? backDue : 0m, currentCall, backCall);
        }

        public static string Record(string localAppData, TaxPick? pick, int day)
        {
            if (pick == null || !pick.Paid)
            {
                return "";
            }

            var line = "tax.paid version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " current=" + pick.Current.ToString(CultureInfo.InvariantCulture)
                + " back=" + pick.Back.ToString(CultureInfo.InvariantCulture)
                + " currentCall=" + pick.CurrentCall
                + " backCall=" + pick.BackCall;
            return AppendOnce(localAppData, line);
        }

        private static string AppendOnce(string localAppData, string line)
        {
            var path = LoadLog.CeoLogPath(localAppData ?? "");
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

            File.AppendAllText(path, line + System.Environment.NewLine);
            return line;
        }
    }

    public sealed class TaxBillWatch
    {
        private static object? _seen;

        private static int _day = -1;

        public bool Due(object? save, int day)
        {
            return save != null && (!ReferenceEquals(_seen, save) || _day != day);
        }

        public void Mark(object? save, int day)
        {
            _seen = save;
            _day = day;
        }
    }
}
