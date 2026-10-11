## Context

`LocalSession`、`WorldState` 与两项功能测试通过全局类名引用读取器；读取器通过固定资源路径读取 JSON。现有 UID 中仅 `scripts/view/test_ground.gd.uid` 没有同名脚本。

## Goals / Non-Goals

**Goals:** 让运行时文件名与类名表达手工区块数据职责，保持 Godot 资源身份与测试发现规则。

**Non-Goals:** 调整地图格式、区块初始化逻辑或玩法。

## Decisions

- 使用 `manual_chunk_catalog.gd` / `ManualChunkCatalog` 和 `manual_chunks.json`，保留“手工内容”语义。
- 测试脚本使用 `test_manual_chunk_catalog.gd`，类名改为 `TestManualChunkCatalog`；更新检查入口。
- 两个被重命名脚本的 UID 内容保持不变，仅移动 UID 文件；删除唯一孤立 UID。

## Risks / Trade-offs

- 路径或类名遗漏会使 Godot 导入失败；运行完整 `tools/check.ps1` 并搜索旧名称。
