# 非空契约与验收记录

## 已收紧的链路

- MockServerService 的默认构造不再传 null 模块；注入构造只接收完整 ServerModules，Config 从模块取得，无法双传不一致配置。
- ServerModules、三个 Actor、三个 Context 的 ConfigService 全部必需非空；LoginContext 必须接收已加载摘要，移除构造兜底。
- ServerModules 在装配时验证配置、仓库及登录工厂，可选角色/地图工厂与诊断只在入口一次补齐。ActorDirectory 的诊断字段非空，创建分片时直接绑定。
- 当前路由成功结果必有账号键，DataResponse.BindUsername 明确非空；错误输出仍可以不绑定账号。
- 空请求、通知与回复回调在入队前拒绝；空角色快照、账号摘要在仓库恢复或写入前拒绝，公开 Context 修改入口也拒绝空输入。

## 全 ServerV2 可空用途审核

| 保留项 | 原因 |
| --- | --- |
| 服务、目录的停止任务和 Actor/连接的消费任务 | 未启动或未停止是实际生命周期状态；不增加假任务和额外状态字段替换它们 |
| Actor 初始化失败、保存汇总中的异常 | 尚未失败或独立分片成功时没有异常 |
| Error 事件 | 可以尚无订阅者，诊断不能影响业务执行 |
| DataSessionSnapshot.Username、连接账号及响应可选绑定 | 匿名、失败或无需重新绑定账号的正常状态 |
| ConnectionSession.Begin 返回值 | 连接无效或代次过期时拒绝本次请求 |
| 连接 Delivery.Packet | 显式保存屏障不包含业务数据 |
| Actor Delivery.Completion、object? 返回通道 | Tell 无回执及业务返回载荷，Ask 仍检查回复类型 |
| 回滚记录 byte[]? | 表示提交前文件不存在，恢复时需要删除目标文件 |
| DataItem.EquipmentSlot | 未穿戴物品没有权威穿戴槽位 |
| SummaryComparer 的可空参数 | 遵守 IEqualityComparer 的相等比较契约，不增加存档字段校验 |
| RepoRole.AfterTableWritten | 仅供测试注入提交中断，默认没有该可选操作 |
| 装配入口的可选工厂与诊断参数 | 接口允许省略，在入口补齐，进入分片后不再判空 |

现有 `_context = default!` 仅用于 INIT 完成后才处理消息的生命周期不变量；TryRead 的 out 抑制仅覆盖返回 false 时没有包，文件目录抑制对应仓库生成的绝对路径。本次没有以抑制操作或测试专用构造绕过必需依赖；测试中 null! 仅用于验收入口拒绝错误参数。

JSON 根 null、公开参数检查与已释放状态检查保留在对应边界。不增加字段完整性、关联、取值范围或配置引用校验。

## 测试

- 编译通过，零警告、零错误。
- V2 普通定向测试 24 项通过；模块测试改用真实配置并在 Godot 宿主运行，原断言保留。
- 普通 .NET 303 项、Godot xUnit 257 项均通过，合计 560 项。原模块测试共 28 项转入 Godot，另新增 1 项保存入口回归测试，未删减原验收场景。
- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests` 已执行三轮：首轮在全部单元测试通过后遇到动画场景 Godot `gchandle.is_released()` 致命错误；后两轮通过动画等前序场景，但被指针场景“光圈未淡出或随摄像机漂移”时序断言阻断。完整脚本本次未一次通过，不能报告全绿。
- 动画和指针场景分别单独重跑通过；脚本未到达的背包/GM、素材、物品移动、装备、丢弃、拾取、UI 边界和角色设置 8 个场景逐一补跑，全部通过。其余前序场景在完整检查中通过。
- 这两类引擎/场景失败已有记录，见 `2026-10-29-add-server-v2-write-back/verification.md`。本次未改动相关场景与旧运行代码，保留失败现象，不为通过脚本修改无关断言。
- change 与 server-v2-account 主规格严格校验通过；全部 34 项主规格常规校验通过（保留其他规格的既有文档警告）。
