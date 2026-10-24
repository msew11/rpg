## Context
日志目前接收 IMessage 与实际写盘字符串，格式化集中在 LogService；发送时为日志额外解码冻结信封。

## Goals / Non-Goals
默认关闭、GM 即时切换、同一 Env 生命周期保留、重启重置；不增加持久化设置或改变业务错误输出。

## Decisions
正式 LogService 构造绑定 EnvService，以只读开关查询控制格式化。测试输出端可绑定环境验证切换，不绑定环境的独立格式测试使用常开输出端。
现有类型化接口足以延迟字符串创建：IMessage 在开关开启后才序列化，IO 复用必要的写盘文本，仅开启后压缩拼接。不增加每次调用的内容闭包。NetService 关闭时跳过日志专用解码，接收解析和文件序列化继续履行业务职责。
GM CheckButton 初始化读取 Env 标记，Toggled 直接修改标记；重建 HUD 时继承同一 Env。

## Risks / Trade-offs
只控制 LogService 管理的通信和 IO 日志，既有错误报告保留。关闭时不补发历史日志。

## Validation
普通测试覆盖默认值、即时切换、关闭时不格式化无效内容、网络和保存正常、重建 Env 重置；真实 HUD 验证开关操作、重建与重启行为。运行完整项目检查。
