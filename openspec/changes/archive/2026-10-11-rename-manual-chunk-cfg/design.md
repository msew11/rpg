## Context

当前 `ManualChunkCatalog` 位于 `scripts/` 根目录，`LocalSession`、`WorldState` 和功能测试按全局类名引用。数据文件 `data/manual_chunks.json` 由固定资源路径读取。

## Goals / Non-Goals

**Goals:** 使用用户指定的 `Cfg` 命名及目录，保持原有 Godot 资源身份和测试行为。

**Non-Goals:** 更改区块数据、初始化流程和数据文件目录。

## Decisions

- 新目录使用 `scripts/cfg/`，脚本名为 `manual_chunk_cfg.gd`，类名为 `ManualChunkCfg`。
- 测试脚本改为 `tests/test_manual_chunk_cfg.gd`；两处脚本 UID 随文件移动且内容不变。
- 更新架构说明、脚本目录规格和检查入口。

## Risks / Trade-offs

- 全局类缓存可能保留旧名；运行 Godot 导入和完整检查确认新类可解析。
