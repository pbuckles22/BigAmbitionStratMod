using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class ShelfPick
    {
        public ShelfPick(
            bool ordered,
            string item,
            int amount,
            int price,
            string street,
            int number,
            string fromStreet,
            int fromNumber,
            string call)
        {
            Ordered = ordered;
            Item = item ?? "";
            Amount = amount;
            Price = price;
            Street = street ?? "";
            Number = number;
            FromStreet = fromStreet ?? "";
            FromNumber = fromNumber;
            Call = call ?? "";
        }

        public bool Ordered { get; }

        public string Item { get; }

        public int Amount { get; }

        public int Price { get; }

        public string Street { get; }

        public int Number { get; }

        public string FromStreet { get; }

        public int FromNumber { get; }

        public string Call { get; }

        public static ShelfPick None()
        {
            return new ShelfPick(false, "", 0, 0, "", 0, "", 0, "");
        }
    }

    public static class StorageShelf
    {
        public const string Item = "ba:itemname_storageshelf";

        public const int Price = 1200;

        public const int Boxes = 16;

        public const string HamptonStreet = "ba:street_seventhavenue";

        public const int HamptonNumber = 13;

        public static ShelfPick Order(
            string? street,
            int number,
            int freeSpace,
            decimal cash,
            bool pending,
            bool inCargo,
            bool seat,
            ApiNote? api)
        {
            var call = api == null ? "" : api.Call("storage shelf");
            if (freeSpace > 0
                || cash < Price
                || pending
                || inCargo
                || !seat
                || string.IsNullOrEmpty(street)
                || number <= 0
                || call.Length == 0)
            {
                return ShelfPick.None();
            }

            return new ShelfPick(
                true,
                Item,
                1,
                Price,
                street,
                number,
                HamptonStreet,
                HamptonNumber,
                call);
        }

        public static string Line(ShelfPick? pick, int day)
        {
            if (pick == null || !pick.Ordered)
            {
                return "";
            }

            return "shelf.ordered version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + pick.Street
                + " number=" + pick.Number.ToString(CultureInfo.InvariantCulture)
                + " item=" + pick.Item
                + " amount=" + pick.Amount.ToString(CultureInfo.InvariantCulture)
                + " price=" + pick.Price.ToString(CultureInfo.InvariantCulture)
                + " from=" + pick.FromStreet
                + " fromNumber=" + pick.FromNumber.ToString(CultureInfo.InvariantCulture)
                + " call=" + pick.Call;
        }

        public static string Record(string localAppData, ShelfPick? pick, int day)
        {
            var line = Line(pick, day);
            if (line.Length == 0)
            {
                return "";
            }

            var path = LoadLog.CeoLogPath(localAppData ?? "");
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            Append(localAppData, line);
            return line;
        }

        public static ShelfAttend Attend(
            string? street,
            int number,
            bool full,
            bool inCargo,
            bool pending,
            decimal cash,
            ShelfSeat? seat,
            ApiNote? api)
        {
            seat ??= ShelfSeat.None("noseat");
            if (inCargo)
            {
                var place = api == null ? "" : api.Call("place shelf");
                return seat.Found && place.Length > 0
                    ? new ShelfAttend(ShelfAction.Place, street, number, true, seat, ShelfPick.None(), place, "")
                    : new ShelfAttend(ShelfAction.Hold, street, number, true, seat, ShelfPick.None(), "", seat.Found ? "nocall" : seat.Reason);
            }

            if (!full || pending)
            {
                return new ShelfAttend(ShelfAction.None, street, number, false, seat, ShelfPick.None(), "", "");
            }

            if (!seat.Found)
            {
                return new ShelfAttend(ShelfAction.Hold, street, number, false, seat, ShelfPick.None(), "", seat.Reason);
            }

            var pick = Order(street, number, freeSpace: 0, cash, pending, inCargo, seat.Found, api);
            return new ShelfAttend(
                pick.Ordered ? ShelfAction.Order : ShelfAction.None,
                street,
                number,
                false,
                seat,
                pick,
                pick.Call,
                "");
        }

        public static string PlaceLine(ShelfAttend? attend, int day)
        {
            if (attend == null || attend.Action != ShelfAction.Place)
            {
                return "";
            }

            return "shelf.placed version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + attend.Street
                + " number=" + attend.Number.ToString(CultureInfo.InvariantCulture)
                + " item=" + Item
                + " x=" + attend.Seat.X.ToString("0.00", CultureInfo.InvariantCulture)
                + " z=" + attend.Seat.Z.ToString("0.00", CultureInfo.InvariantCulture)
                + " yaw=" + attend.Seat.Yaw.ToString("0", CultureInfo.InvariantCulture)
                + " call=" + attend.Call;
        }

        public static string HoldLine(ShelfAttend? attend, int day)
        {
            if (attend == null || attend.Action != ShelfAction.Hold)
            {
                return "";
            }

            return "shelf.held version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + attend.Street
                + " number=" + attend.Number.ToString(CultureInfo.InvariantCulture)
                + " item=" + Item
                + " inCargo=" + (attend.InCargo ? "true" : "false")
                + " reason=" + attend.Reason;
        }

        public static string RecordAttend(string localAppData, ShelfAttend? attend, int day)
        {
            var line = PlaceLine(attend, day);
            if (line.Length == 0)
            {
                line = HoldLine(attend, day);
            }

            if (line.Length == 0)
            {
                return "";
            }

            Append(localAppData, line);
            return line;
        }

        public static ShelfSeat Seat(ShelfFloor? floor, IReadOnlyList<ShelfSpot>? shelves, IReadOnlyList<ShelfSpot>? others)
        {
            if (shelves == null || shelves.Count == 0)
            {
                return ShelfSeat.None("norow");
            }

            if (floor == null || floor.Count == 0)
            {
                return ShelfSeat.None("nogrid");
            }

            var row = LargestRow(shelves);
            foreach (var shelf in row)
            {
                if (!floor.Covers(shelf))
                {
                    return ShelfSeat.None("offgrid");
                }
            }

            var first = row[0];
            var alongWidth = true;
            if (row.Count > 1)
            {
                var (dx, dz) = Delta(row);
                var (lx, lz) = ToLocal(dx, dz, first.Yaw);
                alongWidth = System.Math.Abs(lx) >= System.Math.Abs(lz);
            }

            var step = alongWidth ? first.Width : first.Depth;
            var (ax, az) = ToWorld(alongWidth ? 1f : 0f, alongWidth ? 0f : 1f, first.Yaw);
            row.Sort((a, b) => Project(a, ax, az).CompareTo(Project(b, ax, az)));

            var blockers = new List<ShelfSpot>(shelves);
            if (others != null)
            {
                blockers.AddRange(others);
            }

            for (var i = 0; i + 1 < row.Count; i++)
            {
                var gap = Project(row[i + 1], ax, az) - Project(row[i], ax, az);
                if (gap >= (2f * step) - Touch)
                {
                    var fill = row[i].Moved(ax * step, az * step);
                    if (Clear(floor, fill, blockers))
                    {
                        return ShelfSeat.At(fill);
                    }
                }
            }

            var end = Ends(floor, row, blockers, ax, az, step);
            if (end != null)
            {
                return end;
            }

            if (row.Count == 1)
            {
                var (dx, dz) = ToWorld(0f, 1f, first.Yaw);
                end = Ends(floor, row, blockers, dx, dz, first.Depth);
                if (end != null)
                {
                    return end;
                }
            }

            return ShelfSeat.None("full");
        }

        private static ShelfSeat? Ends(ShelfFloor floor, List<ShelfSpot> row, IReadOnlyList<ShelfSpot> blockers, float ax, float az, float step)
        {
            var high = row[row.Count - 1].Moved(ax * step, az * step);
            if (Clear(floor, high, blockers))
            {
                return ShelfSeat.At(high);
            }

            var low = row[0].Moved(-ax * step, -az * step);
            if (Clear(floor, low, blockers))
            {
                return ShelfSeat.At(low);
            }

            return null;
        }

        internal const float Touch = 0.05f;

        internal const float WallInset = 0.25f;

        private const float Sample = 0.125f;

        private const float RowLinkSteps = 4f;

        private static List<ShelfSpot> LargestRow(IReadOnlyList<ShelfSpot> shelves)
        {
            var seen = new bool[shelves.Count];
            var best = new List<ShelfSpot>();
            for (var start = 0; start < shelves.Count; start++)
            {
                if (seen[start])
                {
                    continue;
                }

                var group = new List<ShelfSpot>();
                var queue = new Queue<int>();
                queue.Enqueue(start);
                seen[start] = true;
                while (queue.Count > 0)
                {
                    var at = queue.Dequeue();
                    group.Add(shelves[at]);
                    for (var next = 0; next < shelves.Count; next++)
                    {
                        if (!seen[next] && Linked(shelves[at], shelves[next]))
                        {
                            seen[next] = true;
                            queue.Enqueue(next);
                        }
                    }
                }

                if (group.Count > best.Count)
                {
                    best = group;
                }
            }

            return best;
        }

        private static bool Linked(ShelfSpot a, ShelfSpot b)
        {
            if (!SameYaw(a.Yaw, b.Yaw))
            {
                return false;
            }

            var (ax, az) = a.Center();
            var (bx, bz) = b.Center();
            var (lx, lz) = ToLocal(bx - ax, bz - az, a.Yaw);
            var sideBySide = System.Math.Abs(lz) <= Touch && System.Math.Abs(lx) <= (RowLinkSteps * a.Width) + Touch;
            var backToBack = System.Math.Abs(lx) <= Touch && System.Math.Abs(lz) <= (RowLinkSteps * a.Depth) + Touch;
            return sideBySide || backToBack;
        }

        private static bool SameYaw(float a, float b)
        {
            var diff = System.Math.Abs(Normalize(a) - Normalize(b));
            return diff <= 1f || diff >= 359f;
        }

        private static float Normalize(float yaw)
        {
            var value = yaw % 360f;
            return value < 0f ? value + 360f : value;
        }

        private static (float dx, float dz) Delta(List<ShelfSpot> row)
        {
            var bestDx = 0f;
            var bestDz = 0f;
            var bestLength = -1f;
            for (var i = 0; i < row.Count; i++)
            {
                for (var j = i + 1; j < row.Count; j++)
                {
                    var (ix, iz) = row[i].Center();
                    var (jx, jz) = row[j].Center();
                    var dx = jx - ix;
                    var dz = jz - iz;
                    var length = (dx * dx) + (dz * dz);
                    if (length > bestLength)
                    {
                        bestLength = length;
                        bestDx = dx;
                        bestDz = dz;
                    }
                }
            }

            return (bestDx, bestDz);
        }

        private static float Project(ShelfSpot spot, float ax, float az)
        {
            var (cx, cz) = spot.Center();
            return (cx * ax) + (cz * az);
        }

        private static bool Clear(ShelfFloor floor, ShelfSpot seat, IReadOnlyList<ShelfSpot> blockers)
        {
            foreach (var (x, z) in Samples(seat, WallInset))
            {
                if (!floor.Free(x, z))
                {
                    return false;
                }
            }

            foreach (var other in blockers)
            {
                foreach (var (x, z) in Samples(seat, Touch))
                {
                    if (other.Contains(x, z, Touch))
                    {
                        return false;
                    }
                }

                foreach (var (x, z) in Samples(other, Touch))
                {
                    if (seat.Contains(x, z, Touch))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        internal static IEnumerable<(float x, float z)> Samples(ShelfSpot spot, float inset)
        {
            var (cx, cz) = spot.Center();
            var halfWidth = System.Math.Max(0f, (spot.Width / 2f) - inset);
            var halfDepth = System.Math.Max(0f, (spot.Depth / 2f) - inset);
            foreach (var lx in Steps(halfWidth))
            {
                foreach (var lz in Steps(halfDepth))
                {
                    var (wx, wz) = ToWorld(lx, lz, spot.Yaw);
                    yield return (cx + wx, cz + wz);
                }
            }
        }

        private static IEnumerable<float> Steps(float half)
        {
            var count = (int)System.Math.Ceiling((2f * half) / Sample);
            if (count <= 0)
            {
                yield return 0f;
                yield break;
            }

            for (var i = 0; i <= count; i++)
            {
                yield return -half + ((2f * half) * i / count);
            }
        }

        internal static (float x, float z) ToWorld(float lx, float lz, float yaw)
        {
            var radians = yaw * System.Math.PI / 180.0;
            var cos = (float)System.Math.Cos(radians);
            var sin = (float)System.Math.Sin(radians);
            return ((lx * cos) + (lz * sin), (-lx * sin) + (lz * cos));
        }

        internal static (float x, float z) ToLocal(float wx, float wz, float yaw)
        {
            var radians = yaw * System.Math.PI / 180.0;
            var cos = (float)System.Math.Cos(radians);
            var sin = (float)System.Math.Sin(radians);
            return ((wx * cos) - (wz * sin), (wx * sin) + (wz * cos));
        }

        private static void Append(string? localAppData, string line)
        {
            var path = LoadLog.CeoLogPath(localAppData ?? "");
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + System.Environment.NewLine);
        }
    }

    public enum ShelfAction
    {
        None,
        Order,
        Place,
        Hold,
    }

    public sealed class ShelfAttend
    {
        public ShelfAttend(
            ShelfAction action,
            string? street,
            int number,
            bool inCargo,
            ShelfSeat seat,
            ShelfPick pick,
            string call,
            string reason)
        {
            Action = action;
            Street = street ?? "";
            Number = number;
            InCargo = inCargo;
            Seat = seat;
            Pick = pick;
            Call = call ?? "";
            Reason = reason ?? "";
        }

        public ShelfAction Action { get; }

        public string Street { get; }

        public int Number { get; }

        public bool InCargo { get; }

        public ShelfSeat Seat { get; }

        public ShelfPick Pick { get; }

        public string Call { get; }

        public string Reason { get; }
    }

    public sealed class ShelfSeat
    {
        private ShelfSeat(bool found, float x, float z, float yaw, string reason)
        {
            Found = found;
            X = x;
            Z = z;
            Yaw = yaw;
            Reason = reason ?? "";
        }

        public bool Found { get; }

        public float X { get; }

        public float Z { get; }

        public float Yaw { get; }

        public string Reason { get; }

        public static ShelfSeat At(ShelfSpot spot)
        {
            return new ShelfSeat(true, spot.X, spot.Z, spot.Yaw, "");
        }

        public static ShelfSeat None(string reason)
        {
            return new ShelfSeat(false, 0f, 0f, 0f, reason);
        }
    }

    public sealed class ShelfSpot
    {
        public ShelfSpot(float x, float z, float yaw, float width, float depth, float offsetX = 0f, float offsetZ = 0f)
        {
            X = x;
            Z = z;
            Yaw = yaw;
            Width = width;
            Depth = depth;
            OffsetX = offsetX;
            OffsetZ = offsetZ;
        }

        public float X { get; }

        public float Z { get; }

        public float Yaw { get; }

        public float Width { get; }

        public float Depth { get; }

        public float OffsetX { get; }

        public float OffsetZ { get; }

        public (float x, float z) Center()
        {
            var (wx, wz) = StorageShelf.ToWorld(OffsetX, OffsetZ, Yaw);
            return (X + wx, Z + wz);
        }

        public ShelfSpot Moved(float dx, float dz)
        {
            return new ShelfSpot(X + dx, Z + dz, Yaw, Width, Depth, OffsetX, OffsetZ);
        }

        public bool Contains(float x, float z, float shrink)
        {
            var (cx, cz) = Center();
            var (lx, lz) = StorageShelf.ToLocal(x - cx, z - cz, Yaw);
            return System.Math.Abs(lx) < (Width / 2f) - shrink
                && System.Math.Abs(lz) < (Depth / 2f) - shrink;
        }
    }

    public sealed class ShelfCell
    {
        public ShelfCell(float x, float z, bool free)
        {
            X = x;
            Z = z;
            IsFree = free;
        }

        public float X { get; }

        public float Z { get; }

        public bool IsFree { get; }
    }

    public sealed class ShelfFloor
    {
        private readonly Dictionary<(int, int), bool> _cells = new Dictionary<(int, int), bool>();

        private readonly float _size;

        private readonly float _originX;

        private readonly float _originZ;

        public ShelfFloor(float size, IEnumerable<ShelfCell>? cells)
        {
            _size = size > 0f ? size : 0.25f;
            var first = true;
            foreach (var cell in cells ?? System.Array.Empty<ShelfCell>())
            {
                if (cell == null)
                {
                    continue;
                }

                if (first)
                {
                    _originX = cell.X;
                    _originZ = cell.Z;
                    first = false;
                }

                var key = Key(cell.X, cell.Z);
                _cells[key] = _cells.TryGetValue(key, out var free) ? free && cell.IsFree : cell.IsFree;
            }
        }

        public int Count => _cells.Count;

        public bool Known(float x, float z)
        {
            return _cells.ContainsKey(Key(x, z));
        }

        public bool Free(float x, float z)
        {
            return _cells.TryGetValue(Key(x, z), out var free) && free;
        }

        public bool Covers(ShelfSpot spot)
        {
            foreach (var (x, z) in StorageShelf.Samples(spot, StorageShelf.WallInset))
            {
                if (!Known(x, z))
                {
                    return false;
                }
            }

            return true;
        }

        private (int, int) Key(float x, float z)
        {
            return (
                (int)System.Math.Floor(((x - _originX) / _size) + 0.5f),
                (int)System.Math.Floor(((z - _originZ) / _size) + 0.5f));
        }
    }
}
