using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System;

namespace Rpg.Server.Modules.Common.Storage;

/// <summary>文件的 JSON 编解码与单文件原子替换，不解释业务字段。</summary>
internal static class StorageJson
{
    /// <summary>独立存档使用 camelCase、数字枚举与可读缩进。</summary>
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    /// <summary>读取完整 JSON；根 null 不能作为加载结果。</summary>
    internal static async ValueTask<T> ReadAsync<T>(string path)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        return await JsonSerializer.DeserializeAsync<T>(stream, Options).ConfigureAwait(false)
            ?? throw new JsonException($"存档根值不能为 null：{path}");
    }
    /// <summary>序列化独立数据，不使用旧服务端格式配置。</summary>
    internal static byte[] Encode<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Options);
    /// <summary>同目录临时文件完整刷盘后替换，失败不截断目标。</summary>
    internal static async ValueTask WriteAsync(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true))
            {
                await stream.WriteAsync(bytes).ConfigureAwait(false);
                stream.Flush(true);
            }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
