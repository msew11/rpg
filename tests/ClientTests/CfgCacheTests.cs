using System.Text.Json.Serialization;
using Rpg.Config;
using Rpg.Config.Cache;

namespace ClientTests;

/// <summary>验证 openspec/specs/client-config-cache 的通用基类、元数据标注和失败契约。</summary>
[Trait("Runtime", "Godot")]
public sealed class CfgCacheTests : IDisposable
{
    /// <summary>本次用例独占的配置与 Schema 目录。</summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "rpg-cfg-cache-" + Guid.NewGuid());
    /// <summary>本次用例使用的文件路径。</summary>
    private string ConfigPath => Path.Combine(_directory, "sample.json");
    /// <summary>本次用例使用的 Schema 路径。</summary>
    private string SchemaPath => Path.Combine(_directory, "sample.schema.json");

    /// <summary>使用宽松 Schema，确保基类自行检查根形状和主键。</summary>
    public CfgCacheTests()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SchemaPath, "true");
    }

    /// <summary>同一模型可作为单例或多例，单例不读取主键元数据，多例按字符串 Code 索引。</summary>
    [Fact]
    public void LoadsObjectsAndArraysUsingDeclaredCacheBase()
    {
        using var single = new SampleSingleCache();
        File.WriteAllText(ConfigPath, """{"code":"alpha","amount":7}""");
        single.Load(ConfigPath, _directory);
        Assert.Equal("alpha", single.Value.Code);
        Assert.Equal(7, single.Value.Amount);
        using var multi = new SampleMultiCache();
        File.WriteAllText(ConfigPath, """[{"code":"beta","amount":2},{"code":"alpha","amount":7}]""");
        multi.Load(ConfigPath, _directory);
        Assert.Equal(2, multi.Items.Count);
        Assert.Equal(7, multi.Items["alpha"].Amount);
        Assert.Equal(2, multi.Items["beta"].Amount);
        Assert.False(multi.Items.ContainsKey("missing"));
        Assert.Throws<InvalidOperationException>(() => multi.Load(ConfigPath, _directory));
    }

    /// <summary>标注后的 JSON 字段名决定键名，不能假设主键叫 id 或 code。</summary>
    [Fact]
    public void RespectsPrimaryKeyJsonPropertyName()
    {
        File.WriteAllText(ConfigPath, """[{"external_key":"key"}]""");
        using var cache = new RenamedKeyCache();
        cache.Load(ConfigPath, _directory);
        Assert.Equal("key", cache.Items["key"].Code);
    }

    /// <summary>单例只接受对象，基类在宽松 Schema 下也拒绝数组、null 和标量。</summary>
    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("42")]
    public void SingleRejectsWrongRoot(string json)
    {
        using var cache = new SampleSingleCache();
        AssertLoadFails(cache, json, "单例配置根必须为对象");
        Assert.Throws<InvalidOperationException>(() => cache.Value);
    }

    /// <summary>多例自行拒绝错误根、非对象行及缺失/null/重复的标注主键。</summary>
    [Theory]
    [InlineData("{}", "多例配置根必须为数组")]
    [InlineData("null", "多例配置根必须为数组")]
    [InlineData("[null]", "每行必须为对象")]
    [InlineData("[1]", "每行必须为对象")]
    [InlineData("[{}]", "主键 code 缺失")]
    [InlineData("""[{"code":null}]""", "主键 code 缺失或为 null")]
    [InlineData("""[{"code":"same"},{"code":"same"}]""", "重复的主键 code：same")]
    public void MultiRejectsInvalidShapeAndKeys(string json, string reason)
    {
        using var cache = new SampleMultiCache();
        AssertLoadFails(cache, json, reason);
        Assert.Throws<InvalidOperationException>(() => cache.Items);
    }

    /// <summary>文件读取、JSON、Schema 文件与校验错误都保留相应文件路径。</summary>
    [Fact]
    public void ReportsMissingFilesBrokenJsonAndSchemaFailures()
    {
        using var cache = new SampleSingleCache();
        var error = Assert.Throws<InvalidDataException>(() => cache.Load(ConfigPath, _directory));
        Assert.Contains(ConfigPath, error.Message);
        AssertLoadFails(cache, "{", "配置文件");
        File.Delete(SchemaPath);
        AssertLoadFails(cache, "{}", SchemaPath);
        File.WriteAllText(SchemaPath, "{");
        AssertLoadFails(cache, "{}", SchemaPath);
        File.WriteAllText(SchemaPath, "false");
        AssertLoadFails(cache, "{}", "Schema 校验失败");
    }

    /// <summary>错误文件标注与主键标注在加载前直接报出类型或主键原因。</summary>
    [Fact]
    public void RejectsInvalidFileAndPrimaryKeyDeclarations()
    {
        Assert.Contains("CfgFile", Assert.Throws<InvalidDataException>(() => new MissingFileCache()).Message);
        Assert.Contains("文件名无效", Assert.Throws<InvalidDataException>(() => new InvalidFileCache()).Message);
        Assert.Contains("CfgKey", Assert.Throws<InvalidDataException>(() => new MissingKeyCache()).Message);
        Assert.Contains("CfgKey", Assert.Throws<InvalidDataException>(() => new MultipleKeysCache()).Message);
        Assert.Contains("Int32", Assert.Throws<InvalidDataException>(() => new WrongKeyTypeCache()).Message);
        Assert.Contains("公开可读实例属性", Assert.Throws<InvalidDataException>(() => new StaticKeyCache()).Message);
    }

    /// <summary>服务释放后，已借出的两类缓存与服务入口同时拒绝读取。</summary>
    [Fact]
    public void ServiceDisposesBorrowedCaches()
    {
        using var config = ConfigTestFixture.LoadDefault();
        var map = config.Map;
        var item = config.Item;
        config.Dispose();
        config.Dispose();
        Assert.Throws<ObjectDisposedException>(() => config.Map);
        Assert.Throws<ObjectDisposedException>(() => config.Item);
        Assert.Throws<ObjectDisposedException>(() => map.Value);
        Assert.Throws<ObjectDisposedException>(() => item.Items);
    }

    /// <summary>地图业务校验仍检查越界和重复格子。</summary>
    [Theory]
    [InlineData("""{"x":1000,"y":0,"tile":"ground"}""", "覆盖格超出地图边界")]
    [InlineData("""{"x":1,"y":1,"tile":"ground"},{"x":1,"y":1,"tile":"ground"}""", "覆盖格坐标重复")]
    public void MapCachePreservesCrossFieldValidation(string tiles, string reason)
    {
        File.WriteAllText(Path.Combine(_directory, "map.json"),
            """{"id":"map","width":1000,"height":1000,"tileSize":32,"defaultTile":"ground","overrides":[""" + tiles + "]}");
        File.WriteAllText(Path.Combine(_directory, "item.json"), "[]");
        File.Copy(Path.Combine(ConfigTestFixture.FindRoot(), "config", "basic.json"), Path.Combine(_directory, "basic.json"));
        using var env = new EnvService("", configDirectory: _directory,
            schemaDirectory: Path.Combine(ConfigTestFixture.FindRoot(), "schemas"));
        // Schema 不放在配置扫描目录，测试专用文件也不参与正式扫描。
        File.Delete(SchemaPath);
        var error = Assert.Throws<InvalidDataException>(() => new ConfigService(env));
        Assert.Contains("map.json", error.Message);
        Assert.Contains(reason.Replace('\\', '/'), error.Message.Replace('\\', '/'));
    }

    /// <summary>将失败输入交给正式加载入口，并检查配置文件与原因。</summary>
    private void AssertLoadFails(CfgCache cache, string json, string reason)
    {
        File.WriteAllText(ConfigPath, json);
        var error = Assert.Throws<InvalidDataException>(() => cache.Load(ConfigPath, _directory));
        Assert.Contains(ConfigPath, error.Message);
        Assert.Contains(reason.Replace('\\', '/'), error.Message.Replace('\\', '/'));
    }

    /// <summary>回收本次用例拥有的临时文件。</summary>
    public void Dispose() => Directory.Delete(_directory, true);

    /// <summary>为通用缓存契约提供字符串主键与普通业务字段。</summary>
    public sealed class SampleCfg
    {
        /// <summary>无需名为 Id 的字符串主键。</summary>
        [CfgKey]
        public string Code { get; init; } = "";
        /// <summary>验证完整对象反序列化的普通数据。</summary>
        public int Amount { get; init; }
    }

    /// <summary>复用同一模型验证单例基类。</summary>
    [CfgFile("sample.json")]
    private sealed class SampleSingleCache : SingleCfgCache<SampleCfg> { }
    /// <summary>复用同一模型验证多例主键契约。</summary>
    [CfgFile("sample.json")]
    private sealed class SampleMultiCache : MultiCfgCache<string, SampleCfg> { }
    /// <summary>验证缺失文件标注的失败契约。</summary>
    private sealed class MissingFileCache : SingleCfgCache<SampleCfg> { }
    /// <summary>验证禁止路径作为文件名。</summary>
    [CfgFile("../sample.json")]
    private sealed class InvalidFileCache : SingleCfgCache<SampleCfg> { }
    /// <summary>验证未标主键的数据契约。</summary>
    private sealed class NoKeyCfg { }
    /// <summary>验证多例缓存不隐式推断主键。</summary>
    [CfgFile("sample.json")]
    private sealed class MissingKeyCache : MultiCfgCache<int, NoKeyCfg> { }
    /// <summary>验证多个主键标注的歧义契约。</summary>
    private sealed class MultipleKeysCfg
    {
        /// <summary>第一项冲突主键。</summary>
        [CfgKey]
        public int First { get; init; }
        /// <summary>第二项冲突主键。</summary>
        [CfgKey]
        public int Second { get; init; }
    }
    /// <summary>验证歧义标注无法建立索引。</summary>
    [CfgFile("sample.json")]
    private sealed class MultipleKeysCache : MultiCfgCache<int, MultipleKeysCfg> { }
    /// <summary>验证主键类型与缓存键类型必须一致。</summary>
    [CfgFile("sample.json")]
    private sealed class WrongKeyTypeCache : MultiCfgCache<int, SampleCfg> { }
    /// <summary>验证静态属性不能作为逐行主键。</summary>
    private sealed class StaticKeyCfg
    {
        /// <summary>错误的静态主键声明。</summary>
        [CfgKey]
        public static int Code => 1;
    }
    /// <summary>验证静态主键契约被拒绝。</summary>
    [CfgFile("sample.json")]
    private sealed class StaticKeyCache : MultiCfgCache<int, StaticKeyCfg> { }
    /// <summary>验证 JSON 字段别名的主键契约。</summary>
    public sealed class RenamedKeyCfg
    {
        /// <summary>与属性名不同的 JSON 主键字段。</summary>
        [CfgKey, JsonPropertyName("external_key")]
        public string Code { get; init; } = "";
    }
    /// <summary>验证主键别名可按统一规则加载。</summary>
    [CfgFile("sample.json")]
    private sealed class RenamedKeyCache : MultiCfgCache<string, RenamedKeyCfg> { }
}
