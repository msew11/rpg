## MODIFIED Requirements

### Requirement: 四字段信封
正式游戏协议 SHALL 在 proto 声明，信封仅包含 generation、request_id、message_type、payload。请求编号 SHALL 非零，响应原样返回，主动通知为零；类型 SHALL 使用显式方向注册。失败 SHALL 使用 ErrorRt 返回错误码和说明，不在成功响应重复 success/error。

#### Scenario: 登录与失败关联
- **WHEN** 合法登录经过字节边界，或请求类型未注册、方向错误或载荷损坏
- **THEN** 成功返回 LoginRt，失败返回 ErrorRt，均保留原请求编号与代次；无法解析的信封向调用方报告错误而不分发
