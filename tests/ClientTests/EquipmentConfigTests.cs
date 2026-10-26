using System.Text.Json.Nodes;
using Rpg.Config;
using Rpg.Const;

namespace ClientTests;

/// <summary>验证 equipment-config、client-item-config 的枚举、列表索引和全表引用启动契约。</summary>
[Trait("Runtime", "Godot")]
public sealed class EquipmentConfigTests : IDisposable
{
    /// <summary>本次验收独占配置目录，不访问用户存档。</summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "rpg-equipment-config-" + Guid.NewGuid());

    /// <summary>复制四张正式配置，使所有用例都经过真实启动加载边界。</summary>
    public EquipmentConfigTests()
    {
        Directory.CreateDirectory(_directory);
        foreach (var name in new[] { "basic", "map", "item", "equipment" })
            File.Copy(Path.Combine(ConfigTestFixture.FindRoot(), "config", name + ".json"), Path.Combine(_directory, name + ".json"));
    }

    /// <summary>创建独立环境并加载配置，不注册全局服务。</summary>
    private ConfigService Load()
    {
        using var env = new EnvService("", configDirectory: _directory,
            schemaDirectory: Path.Combine(ConfigTestFixture.FindRoot(), "schemas"));
        return new ConfigService(env);
    }

    /// <summary>取得待修改的道具数组，保留未知枚举数字供真实解码校验。</summary>
    private JsonArray ReadItems() => JsonNode.Parse(File.ReadAllText(Path.Combine(_directory, "item.json")))!.AsArray();

    /// <summary>保存用例输入，不改变仓库正式配置。</summary>
    private void Write(string name, JsonNode data) => File.WriteAllText(Path.Combine(_directory, name + ".json"), data.ToJsonString());

    /// <summary>同组多条记录不覆盖，复用装备引用合法，查询结果修改和释放都不会泄漏可变缓存。</summary>
    [Fact]
    public void BuildsBothListIndexesAndPreservesIsolation()
    {
        var rows = ReadItems();
        foreach (var id in new[] { 5001, 3001 })
        {
            var sword = rows[0]!.DeepClone();
            sword["id"] = id;
            rows.Add(sword);
        }
        Write("item", rows);
        using var config = Load();
        var item = config.Item;
        Assert.Equal(8, item.GetByCategory(ItemCategory.Weapon).Count);
        Assert.Single(item.GetByCategory(ItemCategory.Armor));
        var swords = item.GetByCategoryAndSubCategory(ItemCategory.Weapon, ItemSubCategory.Sword);
        Assert.IsType<List<Rpg.Config.Data.ItemCfg>>(swords);
        Assert.Equal(new[] { 1012, 5001, 3001 }, swords.Select(row => row.Id));
        Assert.All(swords, row => Assert.Same(item.Items[row.Id], row));
        swords.Clear();
        item.GetByCategory(ItemCategory.Weapon).Clear();
        Assert.Equal(3, item.GetByCategoryAndSubCategory(ItemCategory.Weapon, ItemSubCategory.Sword).Count);
        Assert.Equal(8, item.GetByCategory(ItemCategory.Weapon).Count);
        Assert.Empty(item.GetByCategory((ItemCategory)999));
        Assert.Empty(item.GetByCategoryAndSubCategory(ItemCategory.Armor, ItemSubCategory.Sword));
        Assert.Equal(1012, item.Items[1012].RelatedId);
        Assert.Equal(1012, config.Equipment.Items[item.Items[1012].RelatedId].Id);
        Assert.NotEqual(item.Items[5001].Id, item.Items[5001].RelatedId);
        var equipment = config.Equipment;
        config.Dispose();
        Assert.Throws<ObjectDisposedException>(() => item.GetByCategory(ItemCategory.Weapon));
        Assert.Throws<ObjectDisposedException>(() => item.GetByCategoryAndSubCategory(ItemCategory.Weapon, ItemSubCategory.Sword));
        Assert.Throws<ObjectDisposedException>(() => equipment.Items);
        Assert.Throws<ObjectDisposedException>(() => config.Equipment);
    }

    /// <summary>普通非负数字也必须属于已定义枚举，合法枚举之间仍须符合大小类归属。</summary>
    [Theory]
    [InlineData("category", 0)]
    [InlineData("category", 999)]
    [InlineData("subCategory", 0)]
    [InlineData("subCategory", 3)]
    [InlineData("subCategory", 999)]
    [InlineData("category", 2)]
    [InlineData("subCategory", 105)]
    public void RejectsUnknownEnumsAndMismatchedTypes(string field, int value)
    {
        var rows = ReadItems();
        rows[0]![field] = value;
        Write("item", rows);
        var error = Assert.Throws<InvalidDataException>(Load);
        Assert.Contains("item.json", error.Message);
        Assert.Contains("1012", error.Message);
        Assert.Contains(field, error.Message);
    }

    /// <summary>非空枚举和关联字段必须存在，null 不能被默认值静默替代。</summary>
    [Theory]
    [InlineData("category", true)]
    [InlineData("category", false)]
    [InlineData("subCategory", true)]
    [InlineData("subCategory", false)]
    [InlineData("relatedId", true)]
    [InlineData("relatedId", false)]
    public void RejectsMissingOrNullRequiredFields(string field, bool remove)
    {
        var rows = ReadItems();
        if (remove) rows[0]!.AsObject().Remove(field);
        else rows[0]![field] = null;
        Write("item", rows);
        var error = Assert.Throws<InvalidDataException>(Load);
        Assert.Contains("item.json", error.Message);
        Assert.Contains("Schema", error.Message);
    }

    /// <summary>装备表检查所有武器与盾牌的引用，不仅检查已映射主手的剑。</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 999)]
    [InlineData(1, 999)]
    [InlineData(2, 999)]
    public void EquipmentCacheRejectsMissingReferencesForEveryEquipmentCategory(int index, int relatedId)
    {
        var rows = ReadItems();
        var id = rows[index]!["id"]!.GetValue<int>();
        rows[index]!["relatedId"] = relatedId;
        Write("item", rows);
        var error = Assert.Throws<InvalidDataException>(Load);
        Assert.Contains("equipment.json", error.Message);
        Assert.Contains("item.json", error.Message);
        Assert.Contains($"id={id}", error.Message);
        Assert.Contains($"relatedId={relatedId}", error.Message);
        Assert.Contains("关联校验", error.Message);
    }

    /// <summary>空表可用且不补建记录，有装备道具时空装备表不能通过。</summary>
    [Fact]
    public void EmptyTablesAreValidOnlyWithoutDanglingEquipmentReferences()
    {
        Write("equipment", new JsonArray());
        Assert.Contains("关联校验", Assert.Throws<InvalidDataException>(Load).Message);
        Write("item", new JsonArray());
        using var config = Load();
        Assert.Empty(config.Item.Items);
        Assert.Empty(config.Item.GetByCategory(ItemCategory.Weapon));
        Assert.Empty(config.Item.GetByCategoryAndSubCategory(ItemCategory.Weapon, ItemSubCategory.Sword));
        Assert.Empty(config.Equipment.Items);
    }

    /// <summary>装备表沿用统一文件、主键和 Schema 错误诊断，不允许把槽位写进装备表。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("[{\"id\":1},{\"id\":1}]")]
    [InlineData("[{\"id\":0}]")]
    [InlineData("[null]")]
    [InlineData("[{\"id\":1,\"slot\":1}]")]
    public void RejectsInvalidEquipmentTables(string? content)
    {
        var path = Path.Combine(_directory, "equipment.json");
        if (content is null) File.Delete(path);
        else File.WriteAllText(path, content);
        Assert.Contains("equipment.json", Assert.Throws<InvalidDataException>(Load).Message);
    }

    /// <summary>回收测试目录及其中的配置副本。</summary>
    public void Dispose() => Directory.Delete(_directory, true);
}
