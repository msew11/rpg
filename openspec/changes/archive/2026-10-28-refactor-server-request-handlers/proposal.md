# Proposal

## Why

请求业务、会话推进和文件访问集中在 MockServer，实体表之外还存在重复的角色与物品业务模型，难以独立验证无 IO 的处理边界。

## What Changes

- 每种请求迁入 Server/Handler/协议文件名目录，公开 Handle(RepoEntityCache, 请求) 并返回对应响应。
- Handler 直接验证和提交实体缓存，通知暂存在内存；加载、发送和定时/关闭落地由外层负责。
- 删除 Server/Data 重复模型，地图对象归 Entity，地图规格和变更归地图模块；保持地图 JSON 格式。
- 保留移动延迟、拾取等待、摘要优先、按表落地与失败重试。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `mock-server`：明确请求 Handler 的实体缓存参数、响应返回值及无 IO 边界。

## Impact

影响 Server、NetService 组装、相关测试和架构文档；不修改客户端协议或地图文件格式。
