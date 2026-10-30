# 进入世界迁移验收记录

验收日期：2026-10-29（America/Los_Angeles）。

## 检查结果

运行 `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests`，退出码 0，输出 `All checks passed.`。

- C# 构建：0 警告、0 错误；Godot 项目导入与默认主场景通过。
- .NET 测试：341 通过，0 失败、0 跳过。
- Godot xUnit：308 通过，0 失败、0 跳过。
- 全部真实节点烟测通过，包括 V2 角色管理、V2 世界进入，以及旧流程的外观、角色管理、地图、图鉴、指针、背包、装备、丢弃拾取、窗口与设置。
- change 和五份受影响主规格的严格校验通过；`git diff --check` 通过。
- 五份 delta 的全部 11 个需求块与同步后的主规格逐块比较一致；既有无关需求保留，新规格 Purpose 来自 delta，主规格没有 delta 操作标题。

曾出现旧测试程序集与运行程序集不一致的 `LoginRt.get_Roles` 加载错误；清理测试项目并串行构建后完整检查通过，随后包含最终修改的完整检查再次通过。

## 完整性、正确性与一致性核对

11 个新增或修改需求、16 个场景均找到实现与测试依据；无需求移除或改名。按下表核对协议、业务、存储、客户端和页面，未发现未实现需求或设计偏差。

| 规格与场景 | 实现依据 | 验收依据 |
| --- | --- | --- |
| 账号页面与草稿、旧结果与非法快照 | SessionService、LoginCache、MainController | ServerV2SessionTests、ServerV2RoleManagementSmoke |
| 内外协议独立构建 | protov2、rpg.csproj、MessageCodec | 实际构建、ServerV2ActorProtocolTests |
| 迟到登录、成功失败与重新认证 | ConnectionSession、ServerRuntime、RequestRouter | 既有 ServerV2NetworkTests；世界测试的 FirstEntrySwitchAndRestartKeepThreeTablesAndNoMapWrites、LateEntryCannotReplaceRoleAfterGenerationReset |
| 独立存档缺失失败、仓库不增加业务校验、中断恢复 | RepoRole、RoleCache、DataCache、RoleActor | ServerV2StorageTests；世界测试的 PartialOrCorruptTablesRejectAndCanBeRepaired、InitialSaveFailureKeepsCandidateUntilFlushRetry |
| 拒绝无归属角色、已有角色重复进入 | LoginActor、EnterWorldHandler、DataCacheRoleSummary | RejectedOwnershipAndFailedSummarySaveNeverCreateRoleFiles、FirstEntrySwitchAndRestartKeepThreeTablesAndNoMapWrites |
| 首次进入恢复、缺表保存失败 | RoleActor、RoleCache、DataCacheRole | FirstEntrySwitchAndRestartKeepThreeTablesAndNoMapWrites、PartialOrCorruptTablesRejectAndCanBeRepaired、InitialSaveFailureKeepsCandidateUntilFlushRetry、UninitializedRoleCannotBeCreatedByQueryOrFlush、MapFailureIsRepairableAndAllLostTablesRecreate |
| 初始地图中心与边缘 | GetWorldEntrySnapshotHandler | FirstEntrySwitchAndRestartKeepThreeTablesAndNoMapWrites、ExistingItemsAndDynamicObjectsAreProjectedWithoutStorageChanges、InvalidGroundStackFailsBeforeConnectionBinding |
| 非法迟到响应、真实按钮与返回 | WorldEntryCache、各不可变展示数据、只读 UI 与 WorldService | WorldEntryRejectsEveryMalformedPartAndCommitsBeforeUi、EntryGenerationAndReadonlyWritesAreIsolated、ServerV2WorldEntrySmoke |
| 本地与真实传输边界 | NetService 的可选 IServerWriteBackTransport 分支、MainController 的离开入口 | V2 两个真实页面烟测的保存故障和关闭保留断言；ServerV2SchedulingTests 与写回测试；代码确认无本地能力直接返回且没有保存消息 |

业务编排保持 Login → Role → World，IO 位于 Actor/Repo 边界，Context → Cache → DataCache → Data 保持成立。已有三表只读加载，首次空表也有待写基线；角色业务投影校验与仓库原始数据读写分开。客户端完整准备后一次接纳，普通监听在缓存之后运行；返回选角不修正摘要。公开协议仅新增 EnterWorld/EnterWorldRt，内部消息不注册外部编解码。

## 交付范围与限制

默认游戏仍使用旧 Server；独立手工入口为 `tests/server_v2_role_management.tscn`，账号 `demo/demo`。支持首次建档、已有存档、地图人物 HUD、只读背包装备、返回切角与重启。移动、物品变更、装备操作、拾取与 GM 等待后续迁移；不含真实网络传输实现或旧存档迁移。强杀允许丢失未落盘变更；部分缺表和损坏不自动覆盖，全部三表丢失会重新建档。

核对结果：完整性、需求实现、场景覆盖、设计一致性与项目代码模式均已检查，无阻塞项，可以归档。
