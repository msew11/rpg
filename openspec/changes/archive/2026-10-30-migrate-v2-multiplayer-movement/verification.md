# 第三批迁移验证报告

日期：2026-10-30。两个顺序change中，网络托管已由4e112c2完成并归档；本报告覆盖多人移动和追加默认账号。

## 功能结果

- 默认入口V2 TCP；启动时MockServer为空，服务器模式点击登录后才创建，服务器账号限定admin/123。纯客户端默认demo1/123，可用demo2/123，多账号列表和编号分区隔离；纯客户端不创建服务器或暴露本地保存。
- 20Hz单轮权威推进、10Hz合并推送；完整位置组包含地图/坐标/目标/速度，公开首次可见状态沿用RoleWorldStatePb，无VisibleRolePb或业务会话隔离字段。
- 控制接管、断线、重新认证、删除及切角经运行时目录和C#邮箱屏障处理；旧调用和输出保留原元数据校验，World不向Role Ask。返回选角保留服务器控制；主动返回登录关闭玩家TCP而保留服务器，被踢保留TCP。
- 客户端世界帧原子接纳、缺口单次重同步；点击预测和20Hz目标合并、保留较新意图，公开远端缓存、100ms插值/最多100ms外推、Action维护玩家实体并复用ActorView。
- 目标、速度、控制和同步序号不落地，重启恢复已保存坐标并静止；物品、装备、拾取及GM写仍禁用。

## 验证映射

| 场景 | 证据 |
| --- | --- |
| 默认账号、TCP启动/复用/失败/代次/本地保存 | ServerV2TcpTests、ServerV2RoleManagementTests、既有Hosting验证 |
| 权威改目标、旧动作、速度、终点、保存重启 | ServerV2MovementTests |
| 接管、旧连接重登、断线移除、单账号多角色 | ServerV2MovementTests、ServerV2WorldEntryTests |
| 运行时迟到控制/认证/代次、原始协议移交 | ServerV2MovementTests、ServerV2NetworkTests、ServerV2ActorProtocolTests |
| 投影失败保留权威状态并重试最新 | ServerV2AoiTests |
| 地图边缘/对角跨块/空块/无交集、公开进入与位置增量 | ServerV2AoiTests、ServerV2MovementTests |
| 非法批次原子拒绝、重复帧、序号缺口、较新全量 | WorldSyncTests |
| 点击预测、合并限流、旧确认保留新目标、远端有限外推 | WorldSyncTests |
| 周期与关闭屏障、失败保存重试 | ServerV2SchedulingTests、ServerV2WriteBackTests、ServerV2PersistenceActorTests及世界进入烟测 |
| 两个独立Godot进程真实TCP互见、预测、远端ActorView移动 | server_v2_multiplayer_pair.tscn，子进程分别admin和demo1 |
| UI创建/删除、世界进入、只读背包装备、保存失败保留、切角重启 | server_v2_role_management_smoke.tscn、server_v2_world_entry_smoke.tscn |

运行 `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests`：.NET 345项、Godot xUnit 321项和所有场景烟测通过，构建0警告0错误。测试宿主改为与Godot项目共用AssemblyLoadContext，避免protobuf类型身份分裂。

## 千连接分组基线

机器：AMD Ryzen 7 9700X，8核/16逻辑处理器，物理内存66,211,885,056字节（约61.7GiB）；Windows NT 10.0.26300，Godot4.7.2.NET、.NET8 Debug/headless。客户端负载生成器与服务器在同一进程，CPU/内存包含两者；连接全部为真实回环TCP，独立临时三表，完成后清理。

50组×20人、每人19个其他可见玩家，组间约4096逻辑像素；持续切换目标，预热10秒，测量60秒。最终复测未与其他测试进程并跑。

- 逻辑轮次p95：**43.4ms**（要求≤50ms）。
- 动作发出至权威世界帧确认p95：**155.4ms**（要求≤200ms）；帧年龄p95为81ms。
- 异常断连0，服务错误0；收到525,000个世界帧，每观察者最终可见19人。
- 输出队列峰值117,351字节，首/末一秒平均均0，无持续增长。
- 同进程CPU折合255.7%单核（约占16逻辑核总量16.0%）；工作集峰值261,853,184字节；GC0/1/2次数[989, 587, 71]。
- 测量内接收411,094,622字节、发送8,874,000字节（含4字节分帧头），约6.53MiB/s服务器下行。完整原始结果见load-grouped.json。

## 密集同区诊断

各档预热3秒、测量10秒，不适用分组基线的通过阈值：

| 同区人数 | 逻辑轮次p95(ms) | 动作确认p95(ms) | 异常断连 | 输出队列峰值(字节) |
| --- | --- | --- | --- | --- |
| 100 | 8.1 | 137.7 | 0 | 0 |
| 250 | 52.3 | 181.7 | 0 | 9044 |
| 500 | 246.9 | 421.0 | 0 | 18072 |
| 1000 | 1043.7 | 1128.2 | 0 | 0 |

100→250→500→1000档均无持续输出排队增长和异常断连，因此完成全部诊断档；没有继续扩大。密集500/1000档确认延迟超出分组基线，1000档约1.1秒。单轮等待合并过期时刻，没有积累无限Tick任务。代码和观测指向密集AOI批次构建、每观察者序列化与GC压力：同区公开位置更新量随人数平方增加；本报告未将该推断表述为独立CPU采样剖析。原始JSON各自保留Passed值，不能把密集诊断误读为分组基线通过。

## 规格、文档与限制

愿景、架构、Server V2说明和路线图已核对，五项delta已同步；本change和所涉五份主规格严格校验通过，`git diff --check`通过。全仓普通规格校验38项通过；全仓`--strict`因其他11份既有规格的Purpose占位/过短及超长要求告警未通过，本次涉及的规格无此告警，未改写无关规格。

正式认证、断线自动重连、V2物品/装备/拾取/GM写业务不在本批范围。密集同区容量不等于千连接分组容量；当前性能结果限于上述机器、回环TCP及测量窗口。

两个change分别归档，在本地develop保留中文Conventional Commit，不推送、不创建远程分支或PR。不提交.godot缓存和build导出。
