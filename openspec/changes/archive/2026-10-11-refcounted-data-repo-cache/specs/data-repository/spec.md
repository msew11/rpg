## MODIFIED Requirements

### Requirement: 管理入口抽象
核心框架 SHALL 提供继承 `RefCounted` 的 C# `DataRepoCache` 抽象基类，定义按仓库 `Type` 获取 `IDataRepo` 的统一入口。该类 SHALL 不要求加入场景树。核心框架 SHALL 不实例化该类，也 SHALL 不注册 AutoLoad 单例。

#### Scenario: 会话私有管理器
- **WHEN** 后续功能实现由 GDScript 会话持有的统一仓库管理器
- **THEN** 它能够继承 `DataRepoCache` 并提供具体仓库查找行为，无须作为场景节点使用
