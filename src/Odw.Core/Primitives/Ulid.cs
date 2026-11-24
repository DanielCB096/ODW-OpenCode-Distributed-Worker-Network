using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace Odw.Core.Primitives;

/// <summary>
/// ULID (Universally Unique Lexicographically Sortable Identifier): 48 bits de tiempo
/// en milisegundos + 80 bits aleatorios, codificados en 26 caracteres Crockford base32.
/// </summary>
public readonly struct Ulid : IEquatable<Ulid>, IComparable<Ulid>
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    private sealed class MonotonicState
    {
        public long LastMs = -1;
        public ushort Hi;
        public ulong Lo;
    }

    private static readonly ConditionalWeakTable<TimeProvider, MonotonicState> States = new();

    private readonly ulong _hi;
    private readonly ulong _lo;

    private Ulid(ulong hi, ulong lo)
    {
        _hi = hi;
        _lo = lo;
    }

    public DateTimeOffset Timestamp => DateTimeOffset.FromUnixTimeMilliseconds((long)(_hi >> 16));

    public bool IsEmpty => _hi == 0 && _lo == 0;

    /// <summary>Genera un ULID monótono: dentro del mismo milisegundo incrementa la parte aleatoria.</summary>
    public static Ulid New(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        var ms = timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        var state = States.GetOrCreateValue(timeProvider);
        lock (state)
        {
            if (ms > state.LastMs)
            {
                Span<byte> random = stackalloc byte[10];
                RandomNumberGenerator.Fill(random);
                state.LastMs = ms;
                state.Hi = BinaryPrimitives.ReadUInt16BigEndian(random);
                state.Lo = BinaryPrimitives.ReadUInt64BigEndian(random[2..]);
            }
            else
            {
                ms = state.LastMs;
                if (!Increment(ref state.Hi, ref state.Lo))
                {
                    state.LastMs = ms + 1;
                    ms = state.LastMs;
                    state.Hi = 0;
                    state.Lo = 0;
                }
            }

            return new Ulid(((ulong)ms << 16) | state.Hi, state.Lo);
        }
    }

    public static Ulid New() => New(TimeProvider.System);

    public static bool TryParse(string? text, out Ulid value)
    {
        value = default;
        if (text is null || text.Length != 26)
        {
            return false;
        }

        var first = Decode(text[0]);
        if (first is < 0 or > 7)
        {
            return false;
        }

        ulong hi = 0;
        ulong lo = 0;
        foreach (var c in text)
        {
            var digit = Decode(c);
            if (digit < 0)
            {
                return false;
            }

            hi = (hi << 5) | (lo >> 59);
            lo = (lo << 5) | (uint)digit;
        }

        value = new Ulid(hi, lo);
        return true;
    }

    public static Ulid Parse(string text)
        => TryParse(text, out var value) ? value : throw new FormatException("ULID inválido.");

    public static bool IsValid(string? text) => TryParse(text, out _);

    public override string ToString()
    {
        Span<char> chars = stackalloc char[26];
        for (var i = 0; i < 26; i++)
        {
            var start = 125 - (5 * i);
            var count = Math.Min(5, 128 - start);
            var chunk = ReadBits(_hi, _lo, start, count);
            chars[i] = Alphabet[(int)chunk];
        }

        return new string(chars);
    }

    public bool Equals(Ulid other) => _hi == other._hi && _lo == other._lo;

    public override bool Equals(object? obj) => obj is Ulid other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(_hi, _lo);

    public int CompareTo(Ulid other)
    {
        var byHi = _hi.CompareTo(other._hi);
        return byHi != 0 ? byHi : _lo.CompareTo(other._lo);
    }

    public static bool operator ==(Ulid left, Ulid right) => left.Equals(right);

    public static bool operator !=(Ulid left, Ulid right) => !left.Equals(right);

    public static bool operator <(Ulid left, Ulid right) => left.CompareTo(right) < 0;

    public static bool operator >(Ulid left, Ulid right) => left.CompareTo(right) > 0;

    private static bool Increment(ref ushort hi, ref ulong lo)
    {
        if (lo != ulong.MaxValue)
        {
            lo++;
            return true;
        }

        lo = 0;
        if (hi != ushort.MaxValue)
        {
            hi++;
            return true;
        }

        hi = 0;
        return false;
    }

    private static int Decode(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'A' and <= 'H' => c - 'A' + 10,
        >= 'J' and <= 'K' => c - 'J' + 18,
        >= 'M' and <= 'N' => c - 'M' + 20,
        >= 'P' and <= 'T' => c - 'P' + 22,
        >= 'V' and <= 'Z' => c - 'V' + 27,
        _ => -1,
    };

    private static ulong ReadBits(ulong hi, ulong lo, int start, int count)
    {
        if (count == 0)
        {
            return 0;
        }

        var mask = (1UL << count) - 1;
        if (start >= 64)
        {
            return (hi >> (start - 64)) & mask;
        }

        if (start + count <= 64)
        {
            return (lo >> start) & mask;
        }

        var lowCount = 64 - start;
        var lowPart = (lo >> start) & ((1UL << lowCount) - 1);
        var highPart = (hi & ((1UL << (count - lowCount)) - 1)) << lowCount;
        return lowPart | highPart;
    }
}
