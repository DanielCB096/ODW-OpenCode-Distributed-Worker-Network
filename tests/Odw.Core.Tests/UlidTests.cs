using Odw.Core.Primitives;
using Xunit;

namespace Odw.Core.Tests;

public class UlidTests
{
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public void New_produces_26_crockford_chars()
    {
        var ulid = Ulid.New(TimeProvider.System).ToString();

        Assert.Equal(26, ulid.Length);
        Assert.Matches("^[0-9A-HJKMNP-TV-Z]{26}$", ulid);
    }

    [Fact]
    public void Parse_round_trips()
    {
        var original = Ulid.New(new FixedTimeProvider(TestTasks.CreatedAt)).ToString();

        Assert.True(Ulid.TryParse(original, out var parsed));
        Assert.Equal(original, parsed.ToString());
    }

    [Fact]
    public void Timestamp_is_preserved()
    {
        var instant = new DateTimeOffset(2026, 11, 24, 18, 0, 0, 123, TimeSpan.Zero);
        var ulid = Ulid.New(new FixedTimeProvider(instant));

        Assert.Equal(instant, ulid.Timestamp);
    }

    [Fact]
    public void Ids_are_monotonic_within_the_same_millisecond()
    {
        var provider = new FixedTimeProvider(TestTasks.CreatedAt);
        var previous = Ulid.New(provider).ToString();

        for (var i = 0; i < 200; i++)
        {
            var current = Ulid.New(provider).ToString();
            Assert.True(string.CompareOrdinal(previous, current) < 0, $"{previous} >= {current}");
            previous = current;
        }
    }

    [Fact]
    public void Ids_generated_later_sort_later()
    {
        var early = Ulid.New(new FixedTimeProvider(TestTasks.CreatedAt));
        var late = Ulid.New(new FixedTimeProvider(TestTasks.CreatedAt.AddSeconds(1)));

        Assert.True(early < late);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("01JAZ5Z8Q4XK3V9C2M1N7P6R8")]
    [InlineData("01JAZ5Z8Q4XK3V9C2M1N7P6R8TT")]
    [InlineData("01JAZ5Z8Q4XK3V9C2M1N7P6R8I")]
    [InlineData("01jaz5z8q4xk3v9c2m1n7p6r8t")]
    [InlineData("Z1JAZ5Z8Q4XK3V9C2M1N7P6R8T")]
    public void Invalid_values_are_rejected(string? text)
    {
        Assert.False(Ulid.TryParse(text, out _));
    }

    [Fact]
    public void Parse_and_valid_helpers_work()
    {
        var text = Ulid.New(new FixedTimeProvider(TestTasks.CreatedAt)).ToString();

        Assert.True(Ulid.IsValid(text));
        Assert.Equal(text, Ulid.Parse(text).ToString());
        Assert.Throws<FormatException>(() => Ulid.Parse("bad"));
        Assert.True(Ulid.Parse(new string('0', 26)).IsEmpty);
    }

    [Fact]
    public void Equality_and_ordering_work()
    {
        var value = Ulid.New(new FixedTimeProvider(TestTasks.CreatedAt));
        var same = Ulid.Parse(value.ToString());
        var later = Ulid.New(new FixedTimeProvider(TestTasks.CreatedAt.AddSeconds(1)));

        Assert.True(value == same);
        Assert.False(value != same);
        Assert.True(value.Equals(same));
        Assert.False(value.Equals(null));
        Assert.False(value.Equals("otro"));
        Assert.Equal(value.GetHashCode(), same.GetHashCode());
        Assert.Equal(0, value.CompareTo(same));
        Assert.True(later > value);
        Assert.True(value < later);
        Assert.True(value.CompareTo(later) < 0);
    }

    [Fact]
    public void Zero_ulid_is_valid()
    {
        Assert.True(Ulid.TryParse(new string('0', 26), out var value));
        Assert.Equal(new string('0', 26), value.ToString());
    }
}
