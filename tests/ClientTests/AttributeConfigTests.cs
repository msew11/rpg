using System.Text.Json.Nodes;
using Rpg.Config;

namespace ClientTests;

/// <summary>对应 openspec/specs/entity-attributes：正式配置加载边界与完整等级投放。</summary>
[Trait("Runtime", "Godot")]
public sealed class AttributeConfigTests : IDisposable
{
    /// <summary>每例独立的完整配置目录。</summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "rpg-attributes-" + Guid.NewGuid());
    /// <summary>复制正式配置以测试关联约束。</summary>
    public AttributeConfigTests()
    {
        Directory.CreateDirectory(_root);
        foreach (var path in Directory.GetFiles(Path.Combine(ConfigTestFixture.FindRoot(), "config"), "*.json"))
            File.Copy(path, Path.Combine(_root, Path.GetFileName(path)));
    }
    /// <summary>使用正式加载器，不绕过 Schema。</summary>
    private ConfigService Load()
    {
        using var env = new EnvService("", configDirectory: _root, schemaDirectory: Path.Combine(ConfigTestFixture.FindRoot(), "schemas"));
        return new ConfigService(env);
    }
    /// <summary>完整表允许小数；缺项、未知编号、超精度、越界与缺少初始等级均拒绝。</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("unknown")]
    [InlineData("precision")]
    [InlineData("range")]
    [InlineData("level")]
    public void InvalidLevelMapRejectsStartup(string kind)
    {
        var file = Path.Combine(_root, "role_level.json"); var rows = JsonNode.Parse(File.ReadAllText(file))!.AsArray();
        var map = rows[0]!["attributes"]!.AsObject();
        if (kind == "missing") map.Remove("6");
        if (kind == "unknown") { map.Remove("6"); map["999"] = 0; }
        if (kind == "precision") map["1"] = 180.1234;
        if (kind == "range") map["1"] = 10001;
        if (kind == "level") rows[0]!["level"] = 2;
        File.WriteAllText(file, rows.ToJsonString());
        Assert.Contains("role_level.json", Assert.Throws<InvalidDataException>(Load).Message);
    }
    /// <summary>范围放大不能悄悄破坏配置声明的小数精度。</summary>
    [Fact]
    public void UnrepresentableRangeAndPrecisionAreRejected()
    {
        var file = Path.Combine(_root, "attribute.json"); var rows = JsonNode.Parse(File.ReadAllText(file))!.AsArray();
        rows[0]!["max"] = 1e16;
        File.WriteAllText(file, rows.ToJsonString());
        Assert.Contains("attribute.json", Assert.Throws<InvalidDataException>(Load).Message);
    }
    /// <summary>对应basic-monster-combat：武器必须完整投放整数点伤和正范围攻速，怪物血量遵循公共属性约束。</summary>
    [Theory]
    [InlineData("fraction")]
    [InlineData("range")]
    [InlineData("speed")]
    [InlineData("reversed")]
    [InlineData("missing")]
    [InlineData("monster-health")]
    [InlineData("monster-missing")]
    public void InvalidCombatConfigurationRejectsStartup(string kind)
    {
        var name = kind.StartsWith("monster") ? "monster.json" : "equipment.json";
        var file = Path.Combine(_root,name); var rows = JsonNode.Parse(File.ReadAllText(file))!.AsArray();
        var row = rows[0]!.AsObject();
        if (kind == "fraction") row["baseDamageMin"] = 1.5;
        if (kind == "range") row["baseAttackRange"] = 0;
        if (kind == "speed") row["baseAttackSpeed"] = 0;
        if (kind == "reversed") row["baseDamageMin"] = 99;
        if (kind == "missing") row.Remove("baseDamageMin");
        if (kind == "monster-health") row["attributes"]!["2"] = 1.5;
        if (kind == "monster-missing") row["attributes"]!.AsObject().Remove("10");
        File.WriteAllText(file,rows.ToJsonString());
        Assert.Contains(name,Assert.Throws<InvalidDataException>(Load).Message);
    }
    /// <summary>回收测试配置目录。</summary>
    public void Dispose() => Directory.Delete(_root, true);
}
