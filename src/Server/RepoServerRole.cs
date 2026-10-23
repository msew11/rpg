using System;
using System.IO;
using System.Text.Json;
using Rpg.Server.Data;

namespace Rpg.Server;

/// <summary>按角色隔离 JSON 存档，并以同目录临时文件替换提交状态。</summary>
public sealed class RepoServerRole
{
    /// <summary>服务端存档的绝对目录，由运行入口或测试注入。</summary>
    private readonly string _directory;
    /// <summary>便于调试查看的 JSON 写入设置。</summary>
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    /// <summary>演示账号拥有的角色 ID，不暴露可写集合。</summary>
    public static System.Collections.Generic.IReadOnlyList<long> RoleIds { get; } = Array.AsReadOnly(new long[] { 1001, 1002 });

    /// <summary>绑定存储目录，延迟到具体请求时创建文件。</summary>
    public RepoServerRole(string directory) => _directory = Path.GetFullPath(directory);

    /// <summary>获取已知演示角色的存档路径。</summary>
    public string GetRolePath(long roleId)
    {
        _ = InitialRole(roleId);
        return Path.Combine(_directory, $"{roleId}.json");
    }

    /// <summary>读取并验证角色；仅缺失文件时保存初始状态。</summary>
    public DataServerRole Load(long roleId)
    {
        var path = GetRolePath(roleId);
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            var initial = InitialRole(roleId);
            Save(initial);
            return initial;
        }
        try
        {
            var role = JsonSerializer.Deserialize<DataServerRole>(File.ReadAllText(path))
                ?? throw new InvalidDataException("角色数据为空");
            Validate(role);
            if (role.RoleId != roleId) throw new InvalidDataException("角色 ID 与文件名不符");
            return role;
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            throw new InvalidDataException($"无法读取角色存档：{path}；{error.Message}", error);
        }
    }

    /// <summary>先完整写入并刷盘，再替换目标文件；失败时不破坏已有存档。</summary>
    public void Save(DataServerRole role)
    {
        Validate(role);
        var path = GetRolePath(role.RoleId);
        var temporary = Path.Combine(_directory, $".{role.RoleId}.{Guid.NewGuid():N}.tmp");
        try
        {
            Directory.CreateDirectory(_directory);
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(role, JsonOptions);
                stream.Write(bytes);
                stream.Flush(true);
            }
            File.Move(temporary, path, true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"无法保存角色存档：{path}；{error.Message}", error);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    /// <summary>拒绝无法成为合法角色快照的存档数据。</summary>
    public static void Validate(DataServerRole role)
    {
        ArgumentNullException.ThrowIfNull(role);
        if (role.RoleId <= 0 || string.IsNullOrWhiteSpace(role.Name) || role.Level <= 0 ||
            role.MaxHealth <= 0 || role.Health < 0 || role.Health > role.MaxHealth ||
            role.MaxMana < 0 || role.Mana < 0 || role.Mana > role.MaxMana ||
            role.Experience < 0 || role.NextExperience <= 0 ||
            !float.IsFinite(role.X) || !float.IsFinite(role.Y) || !Enum.IsDefined(role.Gender) ||
            role.Revision < 0 || role.AcknowledgedAction < 0)
            throw new InvalidDataException("角色存档包含无效字段");
    }

    /// <summary>仅为演示账号的两个既有角色提供初始值。</summary>
    private static DataServerRole InitialRole(long roleId) => roleId switch
    {
        1001 => new DataServerRole { RoleId = 1001, Name = "晨星", Level = 5, Health = 86, MaxHealth = 100,
            Mana = 42, MaxMana = 60, Experience = 240, NextExperience = 500, X = 16016, Y = 16016, Gender = ServerRoleGender.Male },
        1002 => new DataServerRole { RoleId = 1002, Name = "流光", Level = 3, Health = 64, MaxHealth = 80,
            Mana = 75, MaxMana = 90, Experience = 120, NextExperience = 300, X = 16144, Y = 16080, Gender = ServerRoleGender.Female },
        _ => throw new ArgumentException("角色不存在", nameof(roleId))
    };
}
