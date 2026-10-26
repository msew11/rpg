using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Json.Schema;
using FileAccess = Godot.FileAccess;

namespace Rpg.Config.Cache;

/// <summary>统一配置文件标注、读取、Schema 校验和生命周期。</summary>
public abstract class CfgCache : IDisposable
{
    /// <summary>当前缓存是否已释放。</summary>
    private bool _isDisposed;
    /// <summary>当前缓存是否已成功加载。</summary>
    private bool _isLoaded;
    /// <summary>最近加载的配置路径，用于整表关联错误诊断。</summary>
    private string? _path;
    /// <summary>供泛型缓存统一使用的 JSON 字段命名规则。</summary>
    protected static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    /// <summary>具体缓存通过特性声明的配置文件名。</summary>
    public string FileName { get; }

    /// <summary>检查具体缓存的文件声明，禁止将目录路径作为文件名。</summary>
    protected CfgCache()
    {
        FileName = GetType().GetCustomAttribute<CfgFileAttribute>()?.FileName
            ?? throw new InvalidDataException($"配置缓存 {GetType().Name} 缺少 CfgFile 标注");
        if (string.IsNullOrWhiteSpace(FileName) || FileName.IndexOfAny(['/', '\\', ':']) >= 0 ||
            !FileName.EndsWith(".json", StringComparison.Ordinal) || FileName.Length <= 5 || FileName.StartsWith('.'))
            throw new InvalidDataException($"配置缓存 {GetType().Name} 的文件名无效：{FileName}");
    }

    /// <summary>统一通过 Godot 读取配置和同名 Schema，失败保留相关文件诊断。</summary>
    internal void Load(string path, string schemaDirectory)
    {
        ThrowIfDisposed();
        if (_isLoaded) throw new InvalidOperationException($"配置已加载：{FileName}");
        _path = path;
        var schemaPath = $"{schemaDirectory}/{Path.GetFileNameWithoutExtension(FileName)}.schema.json";
        try
        {
            if (!FileAccess.FileExists(path)) throw new InvalidDataException("配置文件不存在");
            if (!FileAccess.FileExists(schemaPath)) throw new InvalidDataException($"Schema 不存在：{schemaPath}");
            var schemaText = FileAccess.GetFileAsString(schemaPath);
            JsonSchema schema;
            try { schema = JsonSchema.FromText(schemaText); }
            catch (Exception error) { throw new InvalidDataException($"Schema 无效：{schemaPath}：{error.Message}", error); }
            using var document = JsonDocument.Parse(FileAccess.GetFileAsString(path));
            var result = schema.Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
            if (!result.IsValid)
                throw new InvalidDataException($"Schema 校验失败：{JsonSerializer.Serialize(result)}");
            Read(document.RootElement);
            BuildIndexes();
            _isLoaded = true;
        }
        catch (Exception error)
        {
            ClearIndexes();
            Clear();
            throw new InvalidDataException($"配置文件 {path} 无效：{error.Message}", error);
        }
    }

    /// <summary>由单例或多例基类解析已校验的 JSON 根。</summary>
    protected abstract void Read(JsonElement root);

    /// <summary>整表解析成功后构建派生索引，不读取尚未完成加载的其他表。</summary>
    protected virtual void BuildIndexes() { }

    /// <summary>全部表完成加载后执行整表关联检查，并保留所属文件诊断。</summary>
    internal void ValidateReferences(ConfigService config)
    {
        ThrowIfDisposed();
        if (!_isLoaded) throw new InvalidOperationException($"配置尚未加载：{FileName}");
        try { Validate(config); }
        catch (Exception error)
        {
            throw new InvalidDataException($"配置文件 {_path} 关联校验失败：{error.Message}", error);
        }
    }

    /// <summary>与逐行 Validate 重载区分，借用完整配置服务检查跨表引用。</summary>
    protected virtual void Validate(ConfigService config) { }

    /// <summary>释放或加载失败时清理派生索引。</summary>
    protected virtual void ClearIndexes() { }

    /// <summary>访问缓存数据前拒绝已释放的缓存。</summary>
    protected void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_isDisposed, this);

    /// <summary>释放后阻止已借出的缓存引用继续访问数据。</summary>
    public void Dispose() { _isDisposed = true; ClearIndexes(); Clear(); }

    /// <summary>由单例或多例缓存回收持有的数据。</summary>
    protected abstract void Clear();
}
