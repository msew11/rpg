## Context

`DataRepo<T>` 已提供泛型数据类型约束和抽象 `GetStorageKind()`；枚举有 `Single`、`Multiple` 两个值。现有测试中的具体仓库各自覆写该方法。

## Decisions

- 在 `DataRepo<T>` 与具体仓库之间加入 `DataRepoSingle<T>`、`DataRepoMultiple<T>` 两个抽象层，沿用 `T : IDataRepoData` 约束。
- 两个抽象类使用 `sealed override` 固定形态，避免下级仓库继承后改写返回值。
- 继续保留 `DataRepo<T>` 的原有入口，避免修改已有调用方契约；新基类不承担读写和持久化职责。

## Verification

让核心测试中的单例、多例示例仓库继承新基类，通过 `IDataRepo` 验证数据类型和形态；运行完整检查脚本。
