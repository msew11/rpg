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
账号摘要存储契约 SHALL 以 username.role_summary.json 表示该账号全部角色摘要，不以角色 ID 分散存储账号摘要。V2 服务入口 SHALL 实现客户端 Service 契约，持有构造注入的 ConfigService；NetService SHALL 拥有入口并管理其字节收发与释放。真实摘要 IO、旧数据迁移及客户端默认入口切换 SHALL 留待业务迁移。

#### Scenario: 框架装配
- **WHEN** 通过 NetService 创建 V2 服务链路并收发登录消息后释放 NetService
- **THEN** V2 借用同一 ConfigService，响应经 Net 交付，服务随 Net 释放且 ConfigService 仍有效；默认旧链路可继续登录

#### Scenario: 摘要契约
- **WHEN** 为规范账号构造摘要存储数据与文件名
- **THEN** 数据包含该账号键与摘要集合，文件名是该账号键加 .role_summary.json，框架不写真实存档
