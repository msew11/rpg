using Rpg.Config;
using Rpg.Config.Data;
using Rpg.Services;
using Rpg.Tests;

namespace ClientTests;

/// <summary>验证 openspec/specs/client-item-config 的加载、诊断及只读生命周期场景。</summary>
[Trait("Runtime", "Godot")]
public sealed class ItemConfigTests : IDisposable
{
    /// <summary>仅由本次用例拥有的临时配置和存储目录。</summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "rpg-item-config-" + Guid.NewGuid());
    /// <summary>合法测试行，仅验证加载，不定义正式道具。</summary>
    private const string Row = """{"name":"临时道具","id":1001,"category":2,"subCategory":3,"maxStack":99}""";

    /// <summary>在独立环境准备真实地图与空道具表。</summary>
    public ItemConfigTests()
    {
        Directory.CreateDirectory(Path.Combine(_directory, "config"));
        File.Copy(Path.Combine(ConfigTestFixture.FindRoot(), "config", "map.json"),
            Path.Combine(_directory, "config", "map.json"));
        File.Copy(Path.Combine(ConfigTestFixture.FindRoot(), "config", "basic.json"),
            Path.Combine(_directory, "config", "basic.json"));
        WriteTable("[]");
    }

    /// <summary>多行读取场景：通过完整服务入口按 ID 读取五字段，并保持地图可用。</summary>
    [Fact]
    public void LoadsRowsByIdThroughGameServices()
    {
        WriteTable("[" + Row + "," + """{"name":"临时道具","id":1002,"category":0,"subCategory":0,"maxStack":1}""" + "]");
        using var env = CreateEnv();
        using var registration = GameServicesTestFactory.Register(env);
        var items = GameServices.Config.Item.Items;
        Assert.Equal(2, items.Count);
        Assert.Equal(1001, items[1001].Id);
        Assert.Equal("临时道具", items[1001].Name);
        Assert.Equal(2, items[1001].Category);
        Assert.Equal(3, items[1001].SubCategory);
        Assert.Equal(99, items[1001].MaxStack);
        Assert.Equal(0, items[1002].Category);
        Assert.Equal(0, items[1002].SubCategory);
        Assert.Equal(1, items[1002].MaxStack);
        Assert.False(items.TryGetValue(9999, out _));
        Assert.Equal(1000, GameServices.Config.Map.Value.Width);
    }

    /// <summary>空表场景：临时空配置不自动补建道具。</summary>
    [Fact]
    public void EmptyTableLoadsWithoutInventingItems()
    {
        using var env = CreateEnv();
        using var config = new ConfigService(env);
        Assert.Empty(config.Item.Items);
    }

    /// <summary>验证 client-item-config 的指定测试装备配置，按 ID 读取名称、分类及上限。</summary>
    [Fact]
    public void PackagedTableContainsRequestedTestEquipment()
    {
        using var config = ConfigTestFixture.LoadDefault();
        var expected = new[]
        {
            (1012, "测试长剑", 1, 2), (1013, "测试双手剑", 1, 4), (1014, "测试盾牌", 2, 105),
            (1015, "测试木杖", 1, 16), (1016, "测试枪", 1, 1), (1017, "测试拳刃", 1, 8), (1018, "测试飞刀", 1, 32)
        };
        Assert.Equal(expected.Length, config.Item.Items.Count);
        foreach (var (id, name, category, subCategory) in expected)
        {
            Assert.True(config.Item.Items.TryGetValue(id, out var item));
            Assert.Equal(id, item.Id);
            Assert.Equal(name, item.Name);
            Assert.Equal(category, item.Category);
            Assert.Equal(subCategory, item.SubCategory);
            Assert.Equal(1, item.MaxStack);
        }
    }

    /// <summary>验证 client-item-config 的无效名称场景，缺失、空值与错误类型均被拒绝。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    [InlineData("\"\"")]
    [InlineData("1")]
    public void RejectsInvalidNames(string? nameJson)
    {
        var nameField = nameJson is null ? "" : ",\"name\":" + nameJson;
        WriteTable("[{\"id\":1,\"category\":1,\"subCategory\":1,\"maxStack\":1" + nameField + "}]");
        AssertInvalid("Schema");
    }

    /// <summary>只读与释放场景：映射不能增删改，服务释放后不可读取。</summary>
    [Fact]
    public void MapIsReadOnlyAndDisposedServiceRejectsAccess()
    {
        WriteTable("[" + Row + "]");
        using var env = CreateEnv();
        using var config = new ConfigService(env);
        var cache = config.Item;
        var dictionary = Assert.IsAssignableFrom<IDictionary<int, ItemCfg>>(cache.Items);
        Assert.Throws<NotSupportedException>(() => dictionary.Add(1002, new ItemCfg()));
        Assert.Throws<NotSupportedException>(() => dictionary.Remove(1001));
        Assert.Throws<NotSupportedException>(() => dictionary[1001] = new ItemCfg());
        Assert.Equal(99, config.Item.Items[1001].MaxStack);
        config.Dispose();
        Assert.Throws<ObjectDisposedException>(() => config.Item.Items);
        Assert.Throws<ObjectDisposedException>(() => cache.Items);
    }

    /// <summary>记录无效场景：缺字段、错类型、非法范围和未知字段均阻止启动。</summary>
    [Theory]
    [InlineData("""{"name":"临时道具","id":1,"category":0,"subCategory":0}""")]
    [InlineData("""{"name":"临时道具","id":"1","category":0,"subCategory":0,"maxStack":1}""")]
    [InlineData("""{"name":"临时道具","id":0,"category":0,"subCategory":0,"maxStack":1}""")]
    [InlineData("""{"name":"临时道具","id":1,"category":-1,"subCategory":0,"maxStack":1}""")]
    [InlineData("""{"name":"临时道具","id":1,"category":0,"subCategory":-1,"maxStack":1}""")]
    [InlineData("""{"name":"临时道具","id":1,"category":0,"subCategory":0,"maxStack":0}""")]
    [InlineData("""{"name":"临时道具","id":1,"category":0,"subCategory":0,"maxStack":-1}""")]
    [InlineData("""{"name":"临时道具","id":1,"category":0,"subCategory":0,"maxStack":1.5}""")]
    [InlineData("""{"name":"临时道具","id":2147483648,"category":0,"subCategory":0,"maxStack":1}""")]
    [InlineData("""{"name":"临时道具","id":1,"category":2147483648,"subCategory":0,"maxStack":1}""")]
    [InlineData("""{"name":"临时道具","id":1,"category":0,"subCategory":2147483648,"maxStack":1}""")]
    [InlineData("""{"name":"临时道具","id":1,"category":0,"subCategory":0,"maxStack":2147483648}""")]
    [InlineData("""{"name":"临时道具","id":1,"category":0,"subCategory":0,"maxStack":1,"unexpected":"额外字段"}""")]
    [InlineData("null")]
    public void RejectsInvalidRows(string row)
    {
        WriteTable("[" + row + "]");
        AssertInvalid("Schema");
    }

    /// <summary>记录与文档错误场景：重复编号报告具体 ID，损坏文档包含文件诊断。</summary>
    [Fact]
    public void RejectsDuplicateIdsAndMalformedDocuments()
    {
        WriteTable("[" + Row + "," + Row + "]");
        AssertInvalid("重复的主键 id：1001");
        foreach (var text in new[] { "{", "{}", "null", "{\"kind\":\"item\"}", "{\"kind\":\"item\",\"items\":null}" })
        {
            File.WriteAllText(Path.Combine(_directory, "config", "item.json"), text);
            AssertInvalid("");
        }
    }

    /// <summary>文件契约错误场景：缺表、错误文件名和配置子目录均拒绝。</summary>
    [Fact]
    public void RejectsMissingMisnamedAndNestedTables()
    {
        var path = Path.Combine(_directory, "config", "item.json");
        File.Delete(path);
        AssertInvalid("缺少配置文件");
        WriteTable("[]");
        var renamed = Path.Combine(_directory, "config", "wrong.json");
        File.Move(path, renamed);
        AssertInvalid("item.json", "wrong.json");
        File.Move(renamed, path);
        var nested = Path.Combine(_directory, "config", "nested");
        Directory.CreateDirectory(nested);
        File.Move(path, Path.Combine(nested, "item.json"));
        AssertInvalid("配置目录不允许子目录", "nested");
    }

    /// <summary>用正式环境契约将所有可写数据隔离到本次目录。</summary>
    private EnvService CreateEnv() => new(Path.Combine(_directory, "settings"), Path.Combine(_directory, "server"),
        Path.Combine(_directory, "config"), Path.Combine(ConfigTestFixture.FindRoot(), "schemas"));

    /// <summary>将测试行数组直接写入配置根目录。</summary>
    private void WriteTable(string rows) => File.WriteAllText(Path.Combine(_directory, "config", "item.json"),
        rows);

    /// <summary>用真实 Godot 读取校验失败，并核对文件与原因。</summary>
    private void AssertInvalid(string reason, string fileName = "item.json")
    {
        using var env = CreateEnv();
        var error = Assert.Throws<InvalidDataException>(() => new ConfigService(env));
        Assert.Contains(fileName, error.Message);
        Assert.Contains(reason, error.Message);
    }

    /// <summary>删除本次用例拥有的临时数据。</summary>
    public void Dispose() => Directory.Delete(_directory, true);
}
