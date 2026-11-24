using Odw.Core.Hashing;
using Xunit;

namespace Odw.Core.Tests;

public class CanonicalJsonTests
{
    [Fact]
    public void Object_keys_are_sorted_by_utf16_units()
    {
        var obj = new CanonicalObject();
        obj.Add("b", new CanonicalNumber(2));
        obj.Add("A", new CanonicalNumber(1));
        obj.Add("a", CanonicalNull.Instance);

        Assert.Equal("{\"A\":1,\"a\":null,\"b\":2}", CanonicalJson.Serialize(obj));
    }

    [Fact]
    public void Strings_are_escaped_like_json_stringify()
    {
        var input = "a\"b\\c\nd\te\u0001fñ😀";
        var node = new CanonicalString(input);

        var expected = "\"a\\\"b\\\\c\\nd\\te\\u0001fñ😀\"";
        Assert.Equal(expected, CanonicalJson.Serialize(node));
    }

    [Fact]
    public void Lone_surrogate_is_rejected()
    {
        var node = new CanonicalString("roto\uD800");

        Assert.Throws<FormatException>(() => CanonicalJson.Serialize(node));
    }

    [Fact]
    public void Numbers_are_canonical_decimal()
    {
        var array = new CanonicalArray();
        array.Add(new CanonicalNumber(5m));
        array.Add(new CanonicalNumber(5.0m));
        array.Add(new CanonicalNumber(0.50m));
        array.Add(new CanonicalNumber(0m));
        array.Add(new CanonicalNumber(12m));

        Assert.Equal("[5,5,0.5,0,12]", CanonicalJson.Serialize(array));
    }

    [Fact]
    public void Arrays_preserve_order()
    {
        var array = new CanonicalArray();
        array.Add(new CanonicalString("z"));
        array.Add(new CanonicalString("a"));

        Assert.Equal("[\"z\",\"a\"]", CanonicalJson.Serialize(array));
    }
}
