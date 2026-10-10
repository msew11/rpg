@abstract
class_name DataSource
extends RefCounted

## 具名数据契约。提供方负责存储，并在每次有效写入后发出此信号；
## 消费者修改可变值后须再次写入。
signal value_changed(key: StringName, value: Variant)


## 区分缺失键与值为 null 的已存在键。
@abstract func has_value(key: StringName) -> bool


## 缺失键返回 null；需要区分缺失时先调用 has_value。
@abstract func read_value(key: StringName) -> Variant


## 创建键或改变已存储值时返回 true，并发出一次 value_changed。
## 写入相等值时返回 false，且不发出信号。
@abstract func write_value(key: StringName, value: Variant) -> bool
