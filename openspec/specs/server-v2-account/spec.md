# server-v2-account Specification

## Purpose

定义 V2 账号分片、连接账号绑定以及每账号一份角色摘要的存储契约，区分连接身份、账号身份和角色身份，为后续认证及角色管理提供清晰边界，而不在框架阶段实现这些业务。

## Requirements

### Requirement: 规范账号键与账号分片
V2 SHALL 对登录 username 去除首尾空白并转不变小写，以同一规范键定位账号分片、绑定会话和约定摘要文件名；空值、Windows 不安全文件名及保留设备名 SHALL 拒绝。不同账号 SHALL 隔离摘要状态，相同规范账号 SHALL 共用分片。

#### Scenario: 规范化与隔离
- **WHEN** 两连接分别使用大小写或首尾空白不同的同一账号登录，第三连接使用另一个账号
- **THEN** 前两者定位同一分片，第三者定位独立分片；非法账号在创建分片前失败

### Requirement: 登录后从会话路由
首次登录 SHALL 从 Login.username 定位分片，仅在成功且请求代次仍有效时绑定该连接账号。后续账号请求 SHALL 从服务端会话获取账号键，不要求载荷重复提供 username；未绑定时 SHALL 拒绝。真实认证 SHALL 暂不实现，默认登录返回未实现错误，测试可以注入内存处理器验证链路。

#### Scenario: 成功与失败绑定
- **WHEN** 测试处理器分别返回登录成功和失败，随后投递不含 username 的账号请求
- **THEN** 成功连接定位原账号分片，失败连接不建立绑定且账号请求被拒绝

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
