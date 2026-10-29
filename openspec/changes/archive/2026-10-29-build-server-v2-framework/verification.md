# 验证记录

2026-10-29，Windows / Godot 4.7.2 .NET。

- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests`：通过；构建零警告零错误、资源导入和主场景通过，普通 .NET 299 个、Godot 222 个用例通过，全部现有场景验收通过。完整输出保存在忽略的 `build/server-v2-check.log`。
- 新增 20 个普通用例：初始化屏障、Tell/Ask 顺序与回调、原始初始化错误、通知观察者隔离、唯一分片、独立账号并行、同账号共享与多连接输出隔离、字节复制、代次重置和断开后迟到结果、停服排空、非空摘要投影、错误协议关联、失败登录解除绑定、规范账号与摘要文件名、模块身份与处理器冻结。
- 新增 2 个 Godot 用例：正式配置实例借用、默认 NotImplemented 响应、测试成功响应、Net 只在 Tick 调用线程发布、Net 拥有并释放子服务且保留配置有效、重复释放。
- `openspec validate build-server-v2-framework --strict` 与 `git diff --check`：通过。

## 当前限制

- 默认客户端仍走旧 Server，V2 通过 NetService 的显式服务构造接入。
- V2 不实现认证、角色管理、移动、背包、AOI 或真实网络；默认登录明确返回 NotImplemented。成功处理器仅存在于测试。
- 摘要仓库只有契约，没有文件 IO、延迟写回、事务或旧存档迁移。
- 邮箱目前无容量限制；重连恢复、账号排他策略、远程代次同步及业务版本留给对应后续 change。
- 工作区已有的 consolidate-role-creation-handler 及关联代码、文档改动保留，不纳入本次提交或归档。
