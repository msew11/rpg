using Rpg.Session.Data;

namespace Rpg.World.Actions;

/// <summary>将已验证的 AOI 快照同步到动态世界实体。</summary>
public sealed record SyncMapObjectsAction(DataMapAoiSnapshotPbData? Snapshot) : WorldAction;
