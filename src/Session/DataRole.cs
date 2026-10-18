using System.Collections.Generic;
using Godot;

namespace Rpg.Session;

/// <summary>登录后可选择的角色摘要。</summary>
public sealed record DataRole(long RoleId, string Name, int Level, int Health, int MaxHealth,
    int Mana, int MaxMana, int Experience, int NextExperience, Vector2 Position);

/// <summary>会话持有的最新权威角色状态。</summary>
public sealed record DataRoleState(DataRole Role, Vector2 Position, long Revision, long AcknowledgedAction);

/// <summary>模拟登录的结果及可选角色。</summary>
public sealed record DataLoginResult(bool Success, string Error, IReadOnlyList<DataRole> Roles);
