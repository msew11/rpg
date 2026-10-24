## Why
日志开关仅保存在内存，重新启动游戏需要重复开启。用户希望改用 .env 文件记住选择，同时明确该文件不能通过 LogService 打印，以保持 EnvService 无服务依赖的边界。

## What Changes
- EnvService 启动时直接读取用户数据目录的 .env。
- GM 切换直接保存 RPG_CONSOLE_LOG_ENABLED，跨启动保留；缺失或无效默认关闭。
- 保存保留其他键和注释，不输出任何 .env 读写日志，不访问系统环境变量。
- 写入失败恢复控件并显示原因，保留关闭时日志惰性生成。

## Capabilities
### New Capabilities
无。
### Modified Capabilities
- `console-logging`: 日志标记通过 .env 文件跨启动保留，读写不依赖 LogService。

## Impact
EnvService、GM 错误反馈、文件持久化回归测试及文档；系统与角色 JSON 格式不变。
