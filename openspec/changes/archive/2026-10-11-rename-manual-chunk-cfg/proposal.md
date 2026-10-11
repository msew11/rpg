## Why

用户希望手工区块配置类使用 `Cfg` 命名，并将配置脚本集中在小写的 `scripts/cfg/`。现有 `Catalog` 名称与期望的配置语义不一致，因此需要统一类名、文件名和调用点。

## What Changes

- 将 `ManualChunkCatalog` 改为 `ManualChunkCfg`，脚本移至 `scripts/cfg/manual_chunk_cfg.gd`，连同 UID 一起移动。
- 对应测试改为 `test_manual_chunk_cfg.gd`，更新代码引用、检查入口和文档。

## Capabilities

### Modified Capabilities

- `world-script-boundaries`: 更新手工区块配置脚本的位置与名称。

## Impact

仅调整名称与目录；JSON 数据格式及运行行为不变。
