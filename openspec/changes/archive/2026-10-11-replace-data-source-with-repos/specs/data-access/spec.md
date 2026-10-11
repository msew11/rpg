## REMOVED Requirements

### Requirement: 区分缺失值与空值
**Reason**: 具名键数据源被按类型划分的数据仓库框架取代。
**Migration**: 后续功能通过具体 `DataRepo` 的契约访问数据。

### Requirement: 读取与更新值
**Reason**: 通用 `Variant` 键值读写不属于新的仓库抽象。
**Migration**: 由未来具体仓库定义类型化读写接口。

### Requirement: 通知有效变化
**Reason**: 旧数据源的变更信号不是仓库框架的通用要求。
**Migration**: 若具体仓库需要通知，在该仓库的 change 中定义。
