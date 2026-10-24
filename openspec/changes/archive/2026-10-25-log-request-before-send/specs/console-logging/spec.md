## MODIFIED Requirements
### Requirement: 通信日志与标记着色
客户端 SHALL 在调用传输发送前输出 `[Client][Request]{协议名及消息内容}`，当前代次响应和主动推送解析成功后、发布前分别输出 `[Client][Response]{协议名及消息内容}` 和 `[Client][Notice]{协议名及消息内容}`。仅 `[Request]` SHALL 为绿色，仅 `[Response]` 与 `[Notice]` SHALL 为橙色；来源、内容及 IO 日志 SHALL 保持默认颜色，动态内容不得作为富文本标记执行。

#### Scenario: 冻结请求与有效接收
- **WHEN** 请求入队后被原调用方修改，随后固定帧成功发送并接收响应或推送
- **THEN** 日志记录实际发送的冻结数据及对应类型，响应和推送正确分类，只有指定标记着色

#### Scenario: 无效或取消消息
- **WHEN** 请求在发送前被会话重置取消，或收到损坏数据或旧代次数据
- **THEN** 不输出该消息的成功收发日志

#### Scenario: 同步服务端写盘顺序
- **WHEN** 请求通过同步传输触发服务器成功写盘并返回响应或推送
- **THEN** Request 日志先于 Server IO，随后记录 Notice 或 Response

#### Scenario: 发送尝试失败
- **WHEN** 已打印 Request 后底层传输发送抛出异常
- **THEN** 保留该次冻结请求的 Request 日志并沿用网络错误报告，不将 Request 视为发送成功凭据

