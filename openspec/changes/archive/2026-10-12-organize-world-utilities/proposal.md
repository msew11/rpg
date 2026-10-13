# Proposal

## Why

世界管理器职责拆分后，无状态工具类仍与有状态世界边界类混放在 `scripts/world/` 根目录，目录不能直接反映类的职责。

## What Changes

- 将 `WorldMath`、`WorldEntityTreeCodec`、`WorldMonsterQuery` 及各自的 Godot UID 移到 `scripts/world/utils/`。
- 保持类型名称、方法签名、调用和运行行为不变，并更新架构文档中的目录说明。

## Capabilities

### Modified Capabilities

- `world-script-boundaries`：明确世界内部无状态 C# 工具类的目录归属。

## Impact

影响 `scripts/world/` 下三个工具文件的位置、`docs/architecture.md` 和相关回归检查；不改变存档格式或世界行为。
