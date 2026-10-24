# Proposal

## Why
通信和落盘缺少统一的控制台诊断，需要按客户端请求、响应、推送及双端写文件区分来源。

## What Changes
- 添加 LogService，仅给 Request 标记绿色、Response 和 Notice 标记橙色。
- 网络发送与有效接收输出协议名及 JSON；成功写入设置和角色存档输出目标文件及内容。
- 作为第九个服务显式注册、注入并逆序释放。

## Capabilities
### New Capabilities
- `console-logging`: 网络和文件写入控制台日志格式及局部着色。
### Modified Capabilities
- `game-services`: 增加 Log 全局入口和前置依赖。

## Impact
GameServices、NetService、SettingService、MockServer、RepoServerRole 及相关测试和架构文档。不改变协议或存档格式。
