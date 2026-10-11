## Why

目前每个具体仓库都要重复实现 `GetStorageKind()`，且可能返回与自身预期不符的形态。为单例和多例各提供一个抽象基类，让仓库通过继承关系表达形态。

## What Changes

- 新增 `DataRepoSingle<T>` 和 `DataRepoMultiple<T>`，继承 `DataRepo<T>` 并固定各自的 `GetStorageKind()` 返回值。
- 保留 `DataRepo<T>` 和现有接口；更新核心验收测试与数据层文档。

## Capabilities

### Modified Capabilities

- `data-repository`: 具体仓库可继承对应的形态抽象类。

## Impact

影响 `scripts/core/data/`、`tests/core/`、`docs/data-layer.md` 和数据仓库 spec。不引入具体仓库或数据读写行为。
