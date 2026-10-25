# Design

## Context

现有九服务保持构造拓扑及逆序释放。MockServer 模拟一个观察者，角色和背包采用内存优先、周期落地及正常关闭补写。本次新增动态地图状态和九块 AOI，不加入玩法、AI、美术、传送或真实丢弃。

## Goals / Non-Goals

**Goals:** 数值多地图、唯一服务端对象状态、局部文件落地及跨块一致性、客户端唯一不可变地图缓存，保持现有服务依赖方向。

**Non-Goals:** 真实多人、多进程写盘、跨地图或背包/地图事务、怪物表现和服务器内存分块卸载。

## Decisions

### 配置与角色地图

MapCfg.Id 为正整数并标记 CfgKey，map.json 改为根数组；MapCfgCache 继承 MultiCfgCache<int, MapCfg>。每张地图配置 chunkSizeTiles=32，与绘制块 8 格分别管理。basic.json 的 initialMapId 必须存在。当前地图为 1。角色 MapId 缺失默认为 1，读取不立即迁移，下次 SaveRole 写入；新角色使用 initialMapId。role.proto 追加 map_id=17，不重排旧编号。World 构造时不建图，进入角色按 MapId 创建只读地图组件，离开清空；表现节点跳过退出后等待释放的一帧。

### 所有权与调用方向

MockServer 持有按 ID 寻址的 ServerMap，另持有当前会话 MapAoiTracker。ServerMap 独占对象表、chunk 到对象 ID 的索引及 dirtyChunks，只向下调用 RepoServerMap。RepoServerMap 负责文件加载/提交，不保存第二份业务状态。MapAoiTracker 仅保存观察者身份、中心块、订阅集合和序号，不存对象、不读写文件。地图修改返回 DataServerMapChange，由 MockServer 计算并发送可见通知；不增加反向业务回调、内部事件总线、全局服务或容器。

ServerMap 的对象为不可变 DataServerMapObject，以 GUID 稳定寻址，包含版本、逻辑位置与互斥载荷。怪物保存类型标识；地面道具保存配置编号及合法堆叠数量。完整验证后才更新对象表和索引，跨块同时标记来源及目标；对象移出 AOI 不删除。首次访问一张地图时加载该地图全部动态对象，后续块查询使用内存索引。

### 文件与原子提交

路径为 user://server/maps/<正整数mapId>/manifest.json 和 chunks/<x>_<y>.<commitId>.json。清单保存 formatVersion=1、mapId、完整 layout（地图 ID、尺寸、tileSize、chunkSizeTiles）、commitId 和各非空块引用。块文件保存格式版本、mapId、chunkX/Y、生成该文件的 commitId 与完整对象数组。commitId 为无连字符 GUID；清单可以引用不同提交批次的块版本，未变化块不重写。

Flush 先合并 dirty 块并比较已提交内容，再用同目录临时文件刷盘后写成不可变块文件；空块移除清单引用。全部候选块完成后原子替换清单，作为唯一提交点。之前中断恢复旧清单，之后恢复新清单。只有清单引用的块参与恢复，不扫描最新文件拼接状态。缺失清单为空地图；已有清单损坏、引用缺失、身份/布局不符或重复对象明确报错，不覆盖原数据。地图对象缺少必要字段也视为损坏。

成功提交后清理仅属于本地图且符合本格式、未被引用的旧版本。清理失败汇总诊断，不撤销已完成提交；dirty 保留，后续检测确认相同已提交数据并重试清理，不重复写业务文件。临时文件在写入方法的 finally 中清理；中断遗留临时文件不参与恢复。IO 只记录完成清单提交后的块及清单。单张地图失败继续其他地图和角色，最后汇总。0 禁用周期，正常关闭仍补写；未落地内存不保证异常退出恢复。

### AOI 与协议

world.proto 增加 MapObjectPb（oneof 怪物/地面道具）、MapChunkPb、MapLayoutPb、MapAoiSnapshotPb、MapAoiDeltaPb、MapAoiNotice、ResyncMapAoi/ResyncMapAoiRt。布局用于客户端验证坐标归属，mapId 为数值且同时存在于外层消息与完整快照。对象版本、会话观察者消息序号、磁盘 commitId 分别建模，磁盘版本不进协议。

成功选角先发 SelectRoleRt，再发 sequence=1 的 AOI 快照；地图加载失败不宣布成功。每次业务移动推进后依据实际位置计算中心，订阅中心及周围八个有效块。跨界仅发进入块快照及离开坐标，交集复用；同块不重复发。对象变化根据旧/新块可见性发送完整 upsert 或 ID remove，均不可见不发送、不推进序号。空块也发送空快照。

客户端普通增量要求序号连续，旧序号忽略。缺口后保留最后有效快照、暂停增量，由 Session 入队一次重同步请求；服务端验证请求属于当前角色及地图，只返回权威位置当前九块。快照继续观察者序号，可跨越缺口恢复。重同步失败报告错误并保留旧状态，不静默丢弃。generation 继续隔离同角色旧会话。

### 客户端缓存与集成

Session 持有平级 MapCache，与 RoleCache 同创建/重置/释放。MapCache 继承 CacheBase，PbData 负责不可变复制及完整批次验证；MapCache 再验证布局与本地配置及道具堆叠契约，全部通过才替换快照。MapCache 不发请求，只标记 NeedsResync/ResyncPending。MainController 在普通通知阶段调用 Session.ResyncMapIfNeeded，避免服务注册监听或缓存反向持有网络。World 按角色 MapId 选择静态地图，MapObjects 只读借用 Session.Map，不增加地图对象 ECS、SyncMapAction 或另一份可写集合。

## Risks / Trade-offs

- 首次加载整张地图动态数据，海量对象时需后续内存卸载策略；AOI 查询和发送不扫描全图。
- 清单大小随非空块数增长，每次仍须重写清单；版本块只写变化区域。
- 单进程文件替换沿用现有 OS 持久性边界，不承诺硬件断电或多进程事务。
- 第一版没有跨领域转移。以后背包丢弃必须共同提交 bag 和地图，传送须共同处理角色与跨地图状态。

## Migration Plan

配置和程序一起升级，不保留旧单例 map.json 解析。旧角色 MapId 回填原地图 1，原文件位置不搬迁。现阶段无旧地图动态文件。更新愿景、架构、路线图与 specs，完整检查后归档并本地提交，不推送。
