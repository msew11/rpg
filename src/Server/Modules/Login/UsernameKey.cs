using System;
using System.Linq;

namespace Rpg.Server.Modules.Login;

/// <summary>分片、会话及摘要存储共用的规范账号键。</summary>
public static class UsernameKey
{
    /// <summary>统一大小写与首尾空白，并按 Windows 文件名契约校验。</summary>
    public static string Normalize(string username)
    {
        ArgumentNullException.ThrowIfNull(username);
        var key = username.Trim().ToLowerInvariant();
        if (key.Length == 0 || key.Length + ".role_summary.json".Length > 255 || key.EndsWith('.') ||
            key.Any(character => char.IsControl(character) || "<>:\"/\\|?*".Contains(character)))
            throw new ArgumentException("账号不能为空或包含 Windows 文件名非法字符", nameof(username));
        var stem = key.Split('.')[0].TrimEnd(' ');
        if (stem is "con" or "prn" or "aux" or "nul" or "conin$" or "conout$" ||
            stem.Length == 4 && (stem.StartsWith("com", StringComparison.Ordinal) || stem.StartsWith("lpt", StringComparison.Ordinal)) &&
            "123456789¹²³".Contains(stem[3]))
            throw new ArgumentException("账号不能使用 Windows 保留设备名", nameof(username));
        return key;
    }

    /// <summary>账号摘要的约定文件名，不执行任何磁盘操作。</summary>
    public static string SummaryFileName(string username) => $"{Normalize(username)}.role_summary.json";
}
