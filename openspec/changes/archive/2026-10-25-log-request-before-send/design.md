## Context
MockTransport.Send 同步调用 MockServer.Receive，服务端可能立即提交存档。

## Goals / Non-Goals
目标：Request → Server IO → Notice/Response 的因果顺序。保持冻结、会话取消、颜色和 IO 压缩行为。
不改变传输协议或引入异步日志队列。

## Decisions
在 Tick 解码冻结信封后、调用传输 Send 前输出 Request。Request 表示尝试而非发送成功；传输异常沿用 Error 事件。

## Risks / Trade-offs
发送失败时会出现 Request，因此文档和测试明确尝试语义。

## Validation
真实 MockServer 创建角色应先记录请求，再记录领域存档，最后响应；失败传输仍有请求和错误；运行项目检查。
