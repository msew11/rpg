## MODIFIED Requirements

### Requirement: 账号摘要与服务依赖
账号摘要存储 SHALL 以 username.role_summary.json 表示该账号全部角色摘要，不以角色 ID 分散存储账号摘要。V2 服务入口 SHALL 实现客户端 Service 契约，持有构造注入的 EnvService 与 ConfigService；NetService SHALL 拥有入口并管理其字节收发与释放。摘要真实读写 SHALL 遵循 server-v2-storage；旧数据迁移及客户端默认入口切换 SHALL 留待业务迁移。

#### Scenario: 框架装配
- **WHEN** 通过 NetService 创建 V2 服务链路并收发登录消息后释放 NetService
- **THEN** V2 借用同一 EnvService 与 ConfigService，响应经 Net 交付，服务随 Net 释放且借用服务仍有效；默认旧链路可继续登录

#### Scenario: 摘要契约
- **WHEN** 为规范账号加载及保存摘要
- **THEN** 内存数据包含账号键与摘要集合，文件名是账号键加 .role_summary.json，文件只保存该账号摘要数组
