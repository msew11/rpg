## Why

现有 `DataSource` 以具名键存取任意值，与按数据用途和类型划分仓库、由统一入口管理仓库的目标不符。当前没有正式调用方，可以在核心框架阶段替换这一契约。

## What Changes

- 新增 `DataRepo` 上层抽象，供未来的单例或多例类型化仓库继承；本次不创建任何具体仓库或数据类。
- 新增 `DataRepoCache` 抽象管理入口，供后续在 `develop` 实现统一仓库单例；本次不创建注册表、不配置 AutoLoad。
- **BREAKING** 删除 `DataSource` 契约、仅服务它的测试替身与测试，并更新检查脚本和数据层文档。

## Capabilities

### New Capabilities

- `data-repository`: 定义仓库抽象、仓库管理入口与当前框架边界。

### Modified Capabilities

- `data-access`: 移除旧具名键数据源要求，并退役该能力。

## Impact

影响 `scripts/core/`、`docs/data-layer.md`、`tests/`、`tools/check.ps1` 和对应 OpenSpec 规范。现有运行时代码没有引用 `DataSource`；`develop` 需要通过合并 `main` 继承新的核心接口。
