using System.Collections.Generic;
using Godot;

namespace Rpg.Session;

/// <summary>登录后可选择的角色摘要。</summary>
/// <param name="Gender">角色的基础身体性别；旧构造调用沿用男性默认值。</param>
public sealed record DataRole(long RoleId, string Name, int Level, int Health, int MaxHealth,
    int Mana, int MaxMana, int Experience, int NextExperience, Vector2 Position, RoleGender Gender = RoleGender.Male);

/// <summary>会话持有的最新权威角色状态。</summary>
public sealed record DataRoleState(DataRole Role, Vector2 Position, long Revision, long AcknowledgedAction);

/// <summary>客户端独立持有的登录结果及可选角色摘要。</summary>
public sealed record DataLoginResult(bool Success, string Error, IReadOnlyList<DataRole> Roles);
