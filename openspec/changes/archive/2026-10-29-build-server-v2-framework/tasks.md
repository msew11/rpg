# Tasks

## 1. 协议与运行时

- [x] 1.1 实现独立 protov2 生成、类型化 Actor 邮箱和唯一分片目录；通过编译、邮箱顺序/故障/停止与分片并发测试验证，并记录 Runtime 边界。

## 2. 连接与账号路由

- [x] 2.1 实现连接会话、协议适配、Login/Role/World 模块及账号摘要存储契约；通过字节链路、多连接、规范账号、过期结果和关闭排空测试验证，编写框架接入说明。

## 3. 客户端服务接入

- [x] 3.1 实现持有 ConfigService 的 MockServerService 和 NetService V2 接入；通过 Godot 测试验证配置引用、固定帧收发和释放所有权，同步架构与路线图且保留既有改动。

## 4. 集成检查

- [x] 4.1 运行 tools/check.ps1 -Tests、OpenSpec 严格校验及差异审查，记录结果与限制。

## Workflow follow-up

- 检查完成后同步本 change 的三个新能力并归档，保留仅本次改动的中文约定式本地提交，不推送。
