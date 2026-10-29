# server-v2-network Specification

## Purpose

定义 V2 进程内字节通信的连接、会话与消息关联边界，使多个连接的请求和异步响应互不混淆，并为后续真实传输适配保留明确入口；此阶段只验证基础链路，不连接现有客户端。

## Requirements

### Requirement: 连接绑定会话
服务端 SHALL 为每条新连接生成不复用的连接 ID，并持有对应会话；断开销毁会话，重连创建新身份。连接 ID SHALL 由传输入口绑定，不从客户端载荷采信；不同连接的会话和输出 SHALL 隔离。

#### Scenario: 两个连接收发
- **WHEN** 两条连接使用相同请求编号发送消息
- **THEN** 响应分别进入原连接的输出队列，断开一个连接不影响另一个

### Requirement: 四字段信封
V2 SHALL 在 protov2 声明独立协议，信封仅包含 generation、request_id、message_type、payload。请求编号 SHALL 非零，响应原样返回，主动通知为零；类型 SHALL 使用显式方向注册。失败 SHALL 使用 ErrorRt 返回错误码和说明，不在成功响应重复 success/error。

#### Scenario: 登录与失败关联
- **WHEN** 合法登录经过字节边界，或请求类型未注册、方向错误或载荷损坏
- **THEN** 成功返回 LoginRt，失败返回 ErrorRt，均保留原请求编号与代次；无法解析的信封向调用方报告错误而不分发

### Requirement: 代次与异步结果隔离
会话代次 SHALL 从 1 开始，在同连接重置时递增；重置保留账号绑定并清空旧输出。请求及结果 SHALL 绑定进入时的代次；过期入口、重置后的旧结果及断开后的结果 SHALL 丢弃，不更新新会话身份。

#### Scenario: 迟到登录结果
- **WHEN** 登录处理尚未完成时重置或断开连接，再完成旧登录
- **THEN** 旧结果不绑定账号、不进入当前输出，其他连接照常工作
