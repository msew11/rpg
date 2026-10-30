## MODIFIED Requirements

### Requirement: 内部消息独占移交
进程内调用 SHALL 直接传递生成 proto，不使用客户端信封、额外序列化、中间请求结果包装或逐请求手写接口绑定。请求入队后发送方 SHALL 不再修改，回复返回后接收 Handler SHALL 不留存并修改；消息 SHALL 不携带 Context、Repo 或工作委托，存档 SHALL 保持独立 Data 模型。运行时 SHALL 在邮箱投递元数据中独立携带不可变身份值并统一校验有效性，不包装原始业务 proto，不在业务消息中加入连接、认证或控制隔离字段。

#### Scenario: 直接请求与类型校验
- **WHEN** Actor Ask 内部请求并指定响应类型
- **THEN** 目标 Handler 接收原 proto、直接返回 proto，响应类型不匹配向调用者报错且后续邮箱仍可工作


#### Scenario: 过期运行时投递
- **WHEN** 持有旧控制或认证元数据的回合在排队后执行
- **THEN** 运行时拒绝过期回合，当前身份的原始业务协议仍可正常处理
