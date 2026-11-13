using System;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public static class CeoLedger
    {
        private static Step _rent;
        private static Step _layout;
        private static Step _ops;
        private static Step _contract;
        private static Step _cashier;
        private static bool _hydrated;
        private static string _path = "";

        public static void Clear()
        {
            _rent = default;
            _layout = default;
            _ops = default;
            _contract = default;
            _cashier = default;
            _hydrated = false;
            _path = "";
        }

        public static void Ingest(string? line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return;
            }

            if (line.StartsWith("building.rented ", StringComparison.Ordinal))
            {
                if (TryParsePlace(line, out var street, out var number))
                {
                    _rent = Step.Place(street, number);
                }

                return;
            }

            if (line.StartsWith("layout.skipped ", StringComparison.Ordinal))
            {
                _layout = Step.Skip();
                return;
            }

            if (line.StartsWith("layout.loaded ", StringComparison.Ordinal))
            {
                if (TryParsePlace(line, out var street, out var number))
                {
                    _layout = Step.Place(street, number);
                }

                return;
            }

            if (line.StartsWith("ops.skipped ", StringComparison.Ordinal))
            {
                _ops = Step.Skip();
                return;
            }

            if (line.StartsWith("ops.assigned ", StringComparison.Ordinal))
            {
                if (TryParsePlace(line, out var street, out var number))
                {
                    _ops = Step.Place(street, number);
                }

                return;
            }

            if (line.StartsWith("contract.signed ", StringComparison.Ordinal))
            {
                if (TryParsePlace(line, out var street, out var number))
                {
                    _contract = Step.Place(street, number);
                }

                return;
            }

            if (line.StartsWith("hire.cashier ", StringComparison.Ordinal))
            {
                if (TryParsePlace(line, out var street, out var number))
                {
                    _cashier = Step.Place(street, number);
                }
            }
        }

        public static bool TryRent(out string street, out int number)
        {
            return Read(_rent, out street, out number, out _);
        }

        public static bool TryRent(string localAppData, out string street, out int number)
        {
            Ensure(localAppData);
            return TryRent(out street, out number);
        }

        public static bool TryLayout(out string street, out int number, out bool skipped)
        {
            return Read(_layout, out street, out number, out skipped);
        }

        public static bool TryLayout(string localAppData, out string street, out int number, out bool skipped)
        {
            Ensure(localAppData);
            return TryLayout(out street, out number, out skipped);
        }

        public static bool TryOps(out string street, out int number, out bool skipped)
        {
            return Read(_ops, out street, out number, out skipped);
        }

        public static bool TryOps(string localAppData, out string street, out int number, out bool skipped)
        {
            Ensure(localAppData);
            return TryOps(out street, out number, out skipped);
        }

        public static bool TryContract(out string street, out int number, out bool skipped)
        {
            return Read(_contract, out street, out number, out skipped);
        }

        public static bool TryContract(string localAppData, out string street, out int number, out bool skipped)
        {
            Ensure(localAppData);
            return TryContract(out street, out number, out skipped);
        }

        public static bool TryCashier(out string street, out int number, out bool skipped)
        {
            return Read(_cashier, out street, out number, out skipped);
        }

        public static bool TryCashier(string localAppData, out string street, out int number, out bool skipped)
        {
            Ensure(localAppData);
            return TryCashier(out street, out number, out skipped);
        }

        public static void Ensure(string localAppData)
        {
            var path = LoadLog.CeoLogPath(localAppData ?? "");
            if (_hydrated && string.Equals(_path, path, StringComparison.Ordinal))
            {
                return;
            }

            _path = path;
            _hydrated = true;
            if (!File.Exists(path))
            {
                return;
            }

            foreach (var line in File.ReadAllLines(path))
            {
                Ingest(line);
            }
        }

        private static bool Read(Step step, out string street, out int number, out bool skipped)
        {
            street = step.Street ?? "";
            number = step.Number;
            skipped = step.Skipped;
            return step.Found;
        }

        private static bool TryParsePlace(string line, out string street, out int number)
        {
            street = "";
            number = 0;
            var streetAt = line.IndexOf(" street=", StringComparison.Ordinal);
            var numberAt = line.IndexOf(" number=", StringComparison.Ordinal);
            if (streetAt < 0 || numberAt <= streetAt)
            {
                return false;
            }

            street = line.Substring(streetAt + " street=".Length, numberAt - (streetAt + " street=".Length));
            var rest = line.Substring(numberAt + " number=".Length);
            var stop = rest.IndexOf(' ');
            var token = stop < 0 ? rest : rest.Substring(0, stop);
            return street.Length > 0
                && int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out number)
                && number > 0;
        }

        private struct Step
        {
            public bool Found;
            public bool Skipped;
            public string Street;
            public int Number;

            public static Step Place(string street, int number)
            {
                return new Step
                {
                    Found = true,
                    Skipped = false,
                    Street = street,
                    Number = number,
                };
            }

            public static Step Skip()
            {
                return new Step
                {
                    Found = true,
                    Skipped = true,
                    Street = "",
                    Number = 0,
                };
            }
        }
    }
}
