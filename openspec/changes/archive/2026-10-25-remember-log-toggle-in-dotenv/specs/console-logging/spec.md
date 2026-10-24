## MODIFIED Requirements
### Requirement: 运行期日志开关
GM 面板 SHALL 提供控制台日志开关，状态由 EnvService 管理，启动直接读取 `user://.env` 的 `RPG_CONSOLE_LOG_ENABLED`，GM 切换直接保存该文件，跨启动保留开启或关闭选择。未设置或无效时 SHALL 默认关闭，支持忽略空白、大小写的 true/false、1/0 与成对引号。保存 SHALL 保留其他键和注释、合并重复日志键，通过临时文件替换后更新内存。EnvService SHALL 不依赖 LogService，.env 及其临时文件读写 SHALL 不输出日志，不访问系统环境变量。同一次运行重建 HUD 或切换会话 SHALL 保留标记。关闭时 SHALL 不输出 LogService 管理的通信与 IO 日志，不补发历史日志，不执行日志专用的请求解码、JSON 格式化、压缩与字符串拼接；业务解析、写盘及错误报告 SHALL 继续正常运行。

#### Scenario: 默认关闭与即时切换
- **WHEN** 未设置 .env 日志键时启动，用户在 GM 中开启日志再关闭
- **THEN** 默认无通信与 IO 日志，开启后只记录后续操作，关闭后立即停止且不改变业务结果

#### Scenario: 内存生命周期
- **WHEN** 已开启日志后重建 HUD，再重新启动环境
- **THEN** 同一环境的 HUD 仍显示开启，新环境从 .env 恢复开启；关闭后再次启动也恢复关闭，系统设置和角色存档不包含该标记

#### Scenario: 关闭时避免内容生成
- **WHEN** 关闭日志后执行通信和写盘
- **THEN** 跳过日志专用格式化及解码，仍完成通信与必要的存档序列化

#### Scenario: 保留其他配置且不记录自身写入
- **WHEN** .env 已有其他键、注释和重复的日志键，用户切换日志开关
- **THEN** 保存只更新并合并日志键，其他行保留，不输出任何 .env 读写日志

#### Scenario: 文件保存失败
- **WHEN** GM 切换时 .env 文件保存失败
- **THEN** 内存值与原文件保持不变，控件恢复原状态并显示失败原因
