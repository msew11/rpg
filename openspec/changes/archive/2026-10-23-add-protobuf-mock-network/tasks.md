# Tasks

## 1. 协议与事件基础

- [x] 1.1 实现协议生成、字节路由、队列 NetManager 与缓存优先事件系统，添加协议/事件验收测试，验证 dotnet test 对应测试通过。

## 2. 服务端与会话迁移

- [x] 2.1 实现独立服务端 JSON 存储、请求处理及推送、RoleCache 与 GameSession，添加持久化和会话隔离测试，验证 dotnet test 通过。

## 3. UI 与文档迁移

- [x] 3.1 迁移登录、选角、HUD、世界同步及现有冒烟测试并移除旧源，更新长期文档与路线图，验证客户端及动画冒烟场景通过。

## 4. 集成验收

- [x] 4.1 运行完整 dotnet test、Godot 冒烟和 tools/check.ps1，严格校验 OpenSpec 并核对文档与代码一致。

## Workflow follow-up

- 检查通过后同步规格并归档 change。
- 在 develop 创建中文约定式本地提交，不推送、不创建 PR。
