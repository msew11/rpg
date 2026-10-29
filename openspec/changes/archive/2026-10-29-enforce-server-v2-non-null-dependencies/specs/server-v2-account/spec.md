## MODIFIED Requirements

### Requirement: 账号摘要与服务依赖
账号摘要存储 SHALL 以 username.role_summary.json 表示该账号全部角色摘要，不以角色 ID 分散存储账号摘要。V2 服务入口 SHALL 实现客户端 Service 契约，持有构造注入的 EnvService 与 ConfigService；NetService SHALL 拥有入口并管理其字节收发与释放。业务处理器 SHALL 通过所属 Actor 的上下文读取该服务借用的配置，不自行持有配置依赖或访问全局配置；不同服务的配置 SHALL 隔离，配置释放后读取 SHALL 失败。必需配置和模块依赖 SHALL 在装配入口确定；缺失或已释放的配置 SHALL 在创建分片及执行存储操作前被拒绝，不向内部传递缺失依赖。摘要真实读写 SHALL 遵循 server-v2-storage；旧数据迁移及客户端默认入口切换 SHALL 留待业务迁移。

#### Scenario: 框架装配
- **WHEN** 通过 NetService 创建 V2 服务链路并收发登录消息后释放 NetService
- **THEN** V2 借用同一 EnvService 与 ConfigService，响应经 Net 交付，服务随 Net 释放且借用服务仍有效；默认旧链路可继续登录

#### Scenario: 摘要契约
- **WHEN** 为规范账号加载及保存摘要
- **THEN** 内存数据包含账号键与摘要集合，文件名是账号键加 .role_summary.json，文件只保存该账号摘要数组

#### Scenario: 上下文配置入口
- **WHEN** 两个服务使用不同配置实例执行账号、角色、地图处理器
- **THEN** 各处理器从上下文读取所属服务的同一配置实例，互不串用；停止服务不释放配置，配置所有者释放后再次读取明确失败

#### Scenario: 入口拒绝缺失依赖
- **WHEN** 服务或模块装配入口收到缺失的必需依赖或已释放配置
- **THEN** 同步报告明确参数或释放错误，不创建分片、不执行文件读写；有效配置与完整模块直接进入后续流程
