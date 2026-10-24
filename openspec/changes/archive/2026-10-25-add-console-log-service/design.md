# Design

## Context
网络按固定帧发送冻结字节并解码接收消息，设置与服务端均通过临时文件替换目标。服务禁止访问全局入口。

## Goals / Non-Goals
统一诊断输出，保持现有通信和持久化语义；不添加日志文件、过滤 UI 或网络协议。

## Decisions
- 顺序使用 Env、Log、EventBus、Config、Setting、Net、Session、World、Animation，Log 在消费者释放完后释放。
- Godot PrintRich 仅包裹指定标记；消息 JSON 字符串中的方括号使用 Unicode 转义，数组结构保持原样；IO 使用 Godot Print 纯文本输出，默认文字不着色。消息含协议名和 protobuf JSON，文件日志将实际 JSON 压缩成单行，保留字符串中的空白与转义换行，磁盘仍按既有格式保存。
- Net 构造注入 Log；MockServer 与 Repo 通过可选写盘回调输出，保持服务端独立数据模型及无 Godot 依赖。
- 发送成功后记录冻结消息，当前代次接收解析成功后在发布前记录；失败及旧代次不冒充有效日志。成功替换目标文件后记录 IO，每次提交只记录目标文件一次。
- 测试可注入文本输出端；日志输出端异常由日志服务隔离，避免已提交的存档被报告失败。

## Risks / Trade-offs
频繁移动与大背包会增加控制台输出量，按用户要求完整输出，暂不增加过滤规则。纯文本终端的颜色呈现取决于 Godot 的富文本支持。

## Migration Plan
无需迁移数据；回滚本次提交即可撤销。
