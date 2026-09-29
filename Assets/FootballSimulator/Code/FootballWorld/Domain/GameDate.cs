using System;
using System.Globalization;

namespace FStudio.FootballWorld.Domain
{
    // A civil date in the authored football calendar; no timezone or wall clock.
    public readonly struct GameDate : IComparable<GameDate>, IEquatable<GameDate>
    {
        private readonly DateTime value;
        public int Year => value.Year;
        public int Month => value.Month;
        public int Day => value.Day;
        public GameDate(int year, int month, int day) { value = new DateTime(year, month, day); }
        public DateTime ToDateTime() => value;
        public int CompareTo(GameDate other) => value.CompareTo(other.value);
        public bool Equals(GameDate other) => value == other.value;
        public override bool Equals(object obj) => obj is GameDate other && Equals(other);
        public override int GetHashCode() => value.GetHashCode();
        public override string ToString() => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
