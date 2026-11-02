using System.Text.Json.Nodes;
using Rpg.Config;

namespace ClientTests;

/// <summary>对应 openspec/specs/gm-monster：配置加载、错误诊断及生命周期。</summary>
[Trait("Runtime", "Godot")]
public sealed class MonsterConfigTests : IDisposable
{
    /// <summary>每例独占的完整配置目录。</summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "rpg-monster-config-" + Guid.NewGuid());

    /// <summary>复制正式表，所有错误注入仅发生在临时目录。</summary>
    public MonsterConfigTests()
    {
        Directory.CreateDirectory(_root);
        foreach (var path in Directory.GetFiles(Path.Combine(ConfigTestFixture.FindRoot(), "config"), "*.json"))
            File.Copy(path, Path.Combine(_root, Path.GetFileName(path)));
    }

    /// <summary>正式树妖记录可读，释放后已取得缓存同样拒绝访问。</summary>
    [Fact]
    public void TreantAndDisposedCacheFollowConfigurationContract()
    {
        using var config = Load();
        var cache = config.Monster;
        var tree = Assert.Single(cache.Items).Value;
        Assert.Equal(1001, tree.Id); Assert.Same(tree, cache.Items[1001]); Assert.Equal("树妖", tree.Name);
        Assert.Equal("res://assets/monsters/treant/stand.png", tree.TexturePath);
        Assert.Equal(69, tree.AnchorX); Assert.Equal(90, tree.AnchorY);
        config.Dispose();
        Assert.Throws<ObjectDisposedException>(() => config.Monster);
        Assert.Throws<ObjectDisposedException>(() => cache.Items);
    }

    /// <summary>非正整型或溢出 ID、重复、缺字段、缺图、空名称及画布外锚点在启动阶段拒绝。</summary>
    [Theory]
    [InlineData("duplicate")]
    [InlineData("missing")]
    [InlineData("texture")]
    [InlineData("name")]
    [InlineData("anchor")]
    [InlineData("negative")]
    [InlineData("stringId")]
    [InlineData("zeroId")]
    [InlineData("negativeId")]
    [InlineData("fractionId")]
    [InlineData("overflowId")]
    public void InvalidMonsterConfigurationHasFileDiagnostic(string kind)
    {
        var path = Path.Combine(_root, "monster.json");
        var rows = JsonNode.Parse(File.ReadAllText(path))!.AsArray();
        if (kind == "duplicate") rows.Add(rows[0]!.DeepClone());
        if (kind == "missing") rows[0]!.AsObject().Remove("id");
        if (kind == "texture") rows[0]!["texturePath"] = "res://assets/monsters/missing.png";
        if (kind == "name") rows[0]!["name"] = " ";
        if (kind == "anchor") rows[0]!["anchorX"] = 10000;
        if (kind == "negative") rows[0]!["anchorY"] = -1;
        if (kind == "stringId") rows[0]!["id"] = "1001";
        if (kind == "zeroId") rows[0]!["id"] = 0;
        if (kind == "negativeId") rows[0]!["id"] = -1;
        if (kind == "fractionId") rows[0]!["id"] = 1.5;
        if (kind == "overflowId") rows[0]!["id"] = 2147483648L;
        File.WriteAllText(path, rows.ToJsonString());
        Assert.Contains("monster.json", Assert.Throws<InvalidDataException>(Load).Message);
    }

    /// <summary>使用真实 Godot 配置读取及 Schema 路径。</summary>
    private ConfigService Load()
    {
        using var env = new EnvService("", configDirectory: _root, schemaDirectory: Path.Combine(ConfigTestFixture.FindRoot(), "schemas"));
        return new ConfigService(env);
    }

    /// <summary>回收仅本例拥有的配置目录。</summary>
    public void Dispose() => Directory.Delete(_root, true);
}
