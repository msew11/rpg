# 数据层契约

`scripts/data/data_source.gd` 定义可替换的数据源接口，目前没有正式提供方。功能代码可以依赖 `DataSource`，由具体功能在创建时传入提供方。

| 成员 | 契约 |
| --- | --- |
| `has_value(key: StringName) -> bool` | 判断键是否存在；已写入的 `null` 仍算存在。 |
| `read_value(key: StringName) -> Variant` | 返回当前值；缺失时返回 `null`，需要区分缺失时先调用 `has_value`。 |
| `write_value(key: StringName, value: Variant) -> bool` | 创建或替换值；有效变化返回 `true`，相等值写入返回 `false`。 |
| `value_changed(key: StringName, value: Variant)` | 每次有效写入后由提供方发出一次，携带键和新值；无变化时不发出。 |

提供方负责保存、比较和发出信号。契约不规定生命周期、持久化方式、线程模型或引用型 `Variant` 的深层变化追踪；修改可变对象后需要通过 `write_value` 提交，才能获得变化通知。提供方如支持复杂值，应明确其相等性规则。

`tests/doubles/memory_data_source.gd` 仅用于验证契约，不能作为运行时提供方使用。执行 `powershell -ExecutionPolicy Bypass -File tools/check.ps1` 会运行该测试。

区块功能所需的具体单机内存提供方留待 develop 设计和实现。这里不定义地图数据、坐标、区块标识、回环或玩法状态。
