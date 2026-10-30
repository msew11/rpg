using System.Collections.Immutable;
using Rpg.ServerV2.Runtime;
using Rpg.ServerV2.Modules.Role.Data;
using Rpg.ServerV2.Modules.World.Data;
using Rpg.ServerV2.Modules.Login.Data;

namespace Rpg.ServerV2.Modules.Common.Data;

/// <summary>角色三表在所属邮箱内捕获的共同保存候选。</summary>
internal sealed record DataRoleSave(long RoleId, DataRoleSnapshot? Snapshot, DataRoleTables Tables);
/// <summary>地图及变化块在所属邮箱内捕获的保存候选。</summary>
internal sealed record DataWorldSave(DataWorldLayout Layout, DataWorldSnapshot Snapshot, ImmutableHashSet<DataWorldChunk> Chunks);
/// <summary>账号摘要在所属邮箱内捕获的保存候选。</summary>
internal sealed record DataLoginSave(string Username, ImmutableArray<DataRoleSummary> Summaries, bool Dirty);
/// <summary>只捕获角色，不在邮箱内等待共同保存。</summary>
internal sealed record DataCaptureRoleSave : IActorRequest<DataRoleSave>;
/// <summary>只捕获地图，不在邮箱内等待共同保存。</summary>
internal sealed record DataCaptureWorldSave : IActorRequest<DataWorldSave>;
/// <summary>只捕获账号，不在邮箱内等待共同保存。</summary>
internal sealed record DataCaptureLoginSave : IActorRequest<DataLoginSave>;
/// <summary>共同提交成功后只接受已捕获的角色基线。</summary>
internal sealed record DataAcceptRoleSave(DataRoleSave Save) : IActorRequest<bool>;
/// <summary>共同提交成功后只接受已捕获的地图基线。</summary>
internal sealed record DataAcceptWorldSave(DataWorldSave Save) : IActorRequest<bool>;
/// <summary>共同提交成功后只接受已捕获的账号基线。</summary>
internal sealed record DataAcceptLoginSave(DataLoginSave Save) : IActorRequest<bool>;
