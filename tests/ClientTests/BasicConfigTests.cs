using Rpg.Config;
using Rpg.Config.Data;

namespace ClientTests;

/// <summary>验证 openspec/specs/role-bag 基础容量、Schema、文件诊断和只读缓存生命周期。</summary>
[Trait("Runtime", "Godot")]
public sealed class BasicConfigTests : IDisposable
{
    /// <summary>本次测试独占的配置目录。</summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "rpg-basic-" + Guid.NewGuid());

    /// <summary>复制正式配置，避免修改正式表或用户存档。</summary>
    public BasicConfigTests()
    {
        Directory.CreateDirectory(_directory);
        foreach (var name in new[] { "map", "item", "basic", "equipment" })
            File.Copy(Path.Combine(ConfigTestFixture.FindRoot(), "config", name + ".json"), Path.Combine(_directory, name + ".json"));
    }

    /// <summary>通过正式加载边界构造隔离配置服务。</summary>
    private ConfigService Load() => new(new EnvService("", configDirectory: _directory,
        schemaDirectory: Path.Combine(ConfigTestFixture.FindRoot(), "schemas")));

    /// <summary>默认 40 格、扩容计算及溢出均有明确契约，释放后缓存失效。</summary>
    [Fact]
    public void LoadsCapacityAndRejectsDisposedAccess()
    {
        using var config = Load();
        var cache = config.Basic;
        Assert.Equal(8, cache.Value.BagColumns);
        Assert.Equal(5, cache.Value.BagRows);
        Assert.Equal(40, cache.Value.GetBagCapacity(0));
        Assert.Equal(56, cache.Value.GetBagCapacity(2));
        Assert.Throws<InvalidDataException>(() => cache.Value.GetBagCapacity(-1));
        Assert.Throws<InvalidDataException>(() => cache.Value.GetBagCapacity(int.MaxValue));
        Assert.All(config.Item.Items.Values, item =>
        {
            Assert.StartsWith("item/itempic/", item.IconAssetId);
            Assert.StartsWith("item/item/", item.GroundAssetId);
        });
        config.Dispose();
        Assert.Throws<ObjectDisposedException>(() => config.Basic);
        Assert.Throws<ObjectDisposedException>(() => cache.Value);
    }

    /// <summary>缺表和非法基础值包含文件诊断并阻止启动。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"bagColumns\":0,\"bagRows\":5}")]
    [InlineData("{\"bagColumns\":8,\"bagRows\":-1}")]
    [InlineData("{\"bagColumns\":8,\"bagRows\":1.5}")]
    [InlineData("{\"bagColumns\":2147483647,\"bagRows\":5}")]
    public void InvalidBasicConfigFailsWithFile(string? content)
    {
        var path = Path.Combine(_directory, "basic.json");
        if (content is null) File.Delete(path);
        else File.WriteAllText(path, content);
        Assert.Contains("basic.json", Assert.Throws<InvalidDataException>(() => Load()).Message);
    }

    /// <summary>验证 client-item-config：背包与地面素材字段必须为字符串，缺少时兼容空字符串。</summary>
    [Fact]
    public void IconFieldIsOptionalButMustBeString()
    {
        var path = Path.Combine(_directory, "item.json");
        const string prefix = "\"id\":1,\"name\":\"临时\",\"category\":1,\"subCategory\":2,\"relatedId\":1012,\"maxStack\":1";
        File.WriteAllText(path, "[{" + prefix + "}]");
        using (var config = Load())
        {
            Assert.Empty(config.Item.Items[1].IconAssetId);
            Assert.Empty(config.Item.Items[1].GroundAssetId);
        }
        File.WriteAllText(path, "[{" + prefix + ",\"iconAssetId\":1}]");
        Assert.Contains("item.json", Assert.Throws<InvalidDataException>(() => Load()).Message);
        File.WriteAllText(path, "[{" + prefix + ",\"groundAssetId\":1}]");
        Assert.Contains("item.json", Assert.Throws<InvalidDataException>(() => Load()).Message);
    }

    /// <summary>删除本次测试的临时配置。</summary>
    public void Dispose() => Directory.Delete(_directory, true);
}
