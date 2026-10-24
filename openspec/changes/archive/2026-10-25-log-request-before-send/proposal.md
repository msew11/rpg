## Why
同步 MockTransport 在 Send 内写盘，发送后打印 Request 会让日志顺序与请求因果关系不符。

## What Changes
- 在调用传输 Send 前记录冻结请求，表示发送尝试。
- 发送失败仍保留 Request，并沿用网络错误报告；取消请求不记录。
- 补充真实 MockServer 顺序与失败路径回归测试。

## Capabilities
### New Capabilities
无。
### Modified Capabilities
- `console-logging`: 请求日志在传输发送前输出。

## Impact
NetService、LogService 注释、通信日志测试及架构文档；不改变协议或存档格式。
