# Design

## Context

Login/Role 已有 INIT→RUN→STOP、不可变快照和邮箱内 DataFlush；World 仅有身份。ConfigService 已加载并校验地图配置，StorageJson 提供同目录临时文件刷盘及替换。

## Goals / Non-Goals

实现用户已确认的地图对象全量加载与逐块写回。只新增独立 V2 数据和存储，不引用旧实体、协议、ChunkAt 或 Validate，不改变 Actor 通信。

## Decisions

- DataWorldLayout 复制五项配置；DataWorldObject 是独立不可变记录；Snapshot 直接持有 ImmutableArray。Context 对默认数组归一化，分组后逐项按内容比较，不使用数组引用相等。对象数组顺序保留，同块内容顺序参与比较。
- 以逻辑像素除以 TileSize×ChunkSizeTiles 向下取整生成块键，不校验对象坐标范围或载荷组合。规格来自配置；文件布局必须与其完全一致。
- RepoWorld 只持有路径与诊断，不持有 Context 或业务缓存。每次读取/保存读取清单；缺失清单只在加载时返回内存空地图。清单与块 DTO 必需元数据显式 required，检查版本、身份、布局、块坐标和提交编号；路径仅由坐标与 GUID 提交编号生成。
- 路径为 v2/maps/<mapId>/manifest.json 与 chunks/<x>_<y>.<commitId>.json。清单引用只保存块坐标与提交编号，无任意路径字段。读取只认清单引用，完整加载后返回快照。
- 保存时非空变化块写新版本，未变化块复用引用，变空块移除；全部候选刷盘后替换清单作为唯一提交点。替换前失败不更新 Actor 基线，孤立块不参与恢复。
- 成功后只清理文件名符合本仓库规范的无引用块，未知文件保留；清理失败通过隔离诊断报告且不把已提交结果当失败，下次成功保存重新清理。测试钩子在清单替换前注入中断。
- WorldActor 从 Config.Map.Items 取规格再加载，未知地图不执行仓库 IO；DataFlush 直接 await 保存后接受基线。ServerModules 必需注入 World 仓库，保存 Role→World→Login，各组并行并汇总错误，停止顺序不变。

## Risks / Trade-offs

- 全图对象常驻内存、Commit 全图分组比较 → 本阶段不实现 AOI 或增量加载。
- 同地图仓库依赖所属 Actor 串行调用 → 不提供多进程写者协调或跨 Actor 事务。
- 提交后清理可能失败 → 保留已提交结果，诊断并在后续成功写入重试。
- 强制中断可能留下候选文件 → 清单唯一权威，后续成功保存清理规范候选。
