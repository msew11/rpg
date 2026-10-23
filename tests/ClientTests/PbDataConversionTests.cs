using Godot;
using Rpg.Const;
using Rpg.Net.Message;
using Rpg.Session.Data;

namespace ClientTests;

/// <summary>验证 openspec/specs/client-session 的 PbData 双向转换、字段保留与对象隔离。</summary>
public sealed class PbDataConversionTests
{
    /// <summary>角色摘要双向转换保留全部字段，返回协议的修改不会污染数据。</summary>
    [Theory]
    [InlineData(RoleGenderPb.Male)]
    [InlineData(RoleGenderPb.Female)]
    public void SummaryRoundTripPreservesFieldsAndCreatesIndependentMessages(RoleGenderPb gender)
    {
        var original = new RoleSummaryPb { RoleId = 1002, Name = "摘要角色", Level = 12, Gender = gender };
        var data = RoleSummaryPbData.FromPb(original);
        var result = data.ToPb();
        Assert.Equal(original, result);
        Assert.NotSame(original, result);
        result.Name = "修改返回协议";
        result.Level = 99;
        Assert.Equal(original, data.ToPb());
        original.Name = "修改输入协议";
        Assert.Equal("摘要角色", data.Name);
    }

    /// <summary>世界状态双向转换保留全部字段，输出使用当前权威位置且不共享协议对象。</summary>
    [Theory]
    [InlineData(RoleGenderPb.Male)]
    [InlineData(RoleGenderPb.Female)]
    public void WorldStateRoundTripPreservesFieldsAndUsesAuthoritativePosition(RoleGenderPb gender)
    {
        var original = new RoleWorldStatePb
        {
            RoleId = 1002, Name = "世界角色", Level = 12, Health = 43, MaxHealth = 120,
            Mana = 27, MaxMana = 80, Experience = 16, NextExperience = 250,
            X = 123.5f, Y = 456.25f, Gender = gender, Revision = 9, AcknowledgedAction = 3
        };
        var data = RoleWorldStatePbData.FromPb(original);
        var result = data.ToPb();
        Assert.Equal(original, result);
        Assert.NotSame(original, result);
        result.Health = 0;
        result.X = 0;
        Assert.Equal(original, data.ToPb());
        original.Name = "修改输入协议";
        Assert.Equal("世界角色", data.Name);

        var moved = data with { Position = new Vector2(789, 987) };
        var movedMessage = moved.ToPb();
        Assert.Equal(789f, movedMessage.X);
        Assert.Equal(987f, movedMessage.Y);
        Assert.Equal(new Vector2(123.5f, 456.25f), data.Position);
    }

    /// <summary>手工构造的无效数据不能通过反向转换生成可发送的协议结构。</summary>
    [Fact]
    public void InvalidDataCannotBeConvertedToProtocol()
    {
        Assert.Throws<InvalidDataException>(() => new RoleSummaryPbData(0, "角色", 1, RoleGender.Male).ToPb());
        Assert.Throws<InvalidDataException>(() => new RoleSummaryPbData(1, "角色", 1, (RoleGender)99).ToPb());
        var state = new RoleWorldStatePbData(1, "角色", 1, 10, 10, 5, 10, 0, 20, Vector2.Zero, RoleGender.Male, 1, 0);
        Assert.Throws<InvalidDataException>(() => (state with { Revision = -1 }).ToPb());
        Assert.Throws<InvalidDataException>(() => (state with { Position = new Vector2(float.NaN, 0) }).ToPb());
        Assert.Throws<InvalidDataException>(() => (state with { Health = 11 }).ToPb());
        Assert.Throws<InvalidDataException>(() => (state with { Gender = (RoleGender)99 }).ToPb());
    }
}
