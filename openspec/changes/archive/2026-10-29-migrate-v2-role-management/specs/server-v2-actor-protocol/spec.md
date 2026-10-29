## MODIFIED Requirements

### Requirement: 客户端拒绝内部协议
内部消息 SHALL 不注册客户端 MessageCodec。客户端伪造内部完整类型名 SHALL 被拒绝，且不影响后续外部登录的正常认证。

#### Scenario: 外部投递内部请求
- **WHEN** 客户端通过有效信封发送内部查询消息
- **THEN** 返回关联的未知消息错误，不分发到内部 Handler，后续错误凭据登录返回 INVALID_CREDENTIALS 且不绑定账号
