using System.Text.Json;
using Google.Protobuf;
using Rpg.Config.Cache;
using Rpg.Config.Data;
using Rpg.Protocol;
using Rpg.Session.Data;

namespace ClientTests;

/// <summary>对应 openspec/specs/entity-attributes：map 格式、精度规则和协议隔离。</summary>
public sealed class AttributeValueTests
{
    /// <summary>重复编号和非规范键不能被字典静默覆盖。</summary>
    [Theory]
    [InlineData("{\"1\":2,\"1\":3}")]
    [InlineData("{\"01\":2}")]
    [InlineData("{\"0\":2}")]
    [InlineData("{\"+1\":2}")]
    [InlineData("{\"1\":\"2\"}")]
    [InlineData("{\"1\":1e400}")]
    public void InvalidAttributeMapsFail(string map)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RoleLevelCfg>("{\"Level\":1,\"Attributes\":" + map + "}"));
    }

    /// <summary>数值直接按实际值读取，汇总后舍入、裁剪并规范负零。</summary>
    [Fact]
    public void ActualValuesNormalizeWithoutScale()
    {
        var row = JsonSerializer.Deserialize<RoleLevelCfg>("{\"Level\":1,\"Attributes\":{\"1\":180.125}}")!;
        Assert.Equal(180.125, row.Attributes[1]);
        var definition = new AttributeCfg { Id = 1, Min = 0, Max = 10000, Precision = 3, DisplayDecimals = 2 };
        definition.ValidateValue(0.1 + 0.2);
        Assert.Throws<InvalidDataException>(() => definition.ValidateValue(1.2345));
        Assert.Equal(1.235, definition.Normalize(1.2345));
        Assert.Equal(10000, definition.Normalize(10001));
        Assert.Equal(0, BitConverter.DoubleToInt64Bits(definition.Normalize(-0.00001)));
        Assert.Throws<InvalidDataException>(() => definition.Normalize(double.PositiveInfinity));
    }

    /// <summary>double 原值往返且客户端快照不持有可变 protobuf，属性无业务版本。</summary>
    [Fact]
    public void ProtocolMapAndOptionalGroupsRemainIndependent()
    {
        var attributes = new AttributesPb { Values = { { 1, 180.125 }, { 2, 100 }, { 3, 60 }, { 4, 0.001 }, { 5, 0 }, { 6, 0 } } };
        var parsed = AttributesPb.Parser.ParseFrom(attributes.ToByteArray());
        var snapshot = AttributesPbData.FromPb(parsed);
        parsed.Values[1] = 999;
        Assert.Equal(180.125, snapshot.Values[1]);
        Assert.Equal(attributes, snapshot.ToPb());
        Assert.Equal(new[] { "values" }, AttributesPb.Descriptor.Fields.InDeclarationOrder().Select(field => field.Name));
        var update = new RoleWorldUpdatePb { RoleId = 1, Status = new() { Health = 0, MaxHealth = 100 }, Equipment = new() };
        var roundtrip = RoleWorldUpdatePb.Parser.ParseFrom(update.ToByteArray());
        Assert.NotNull(roundtrip.Status); Assert.NotNull(roundtrip.Equipment); Assert.Null(roundtrip.Position);
        Assert.Equal(new[] { "role_id" }, EnterWorld.Descriptor.Fields.InDeclarationOrder().Select(field => field.Name));
    }
}
