# Tasks

## 1. 统一入口与缓存订阅

- [x] 1.1 统一全部服务入口和 IsInitialized，移除 Cache 的事件依赖及 Session 传参；更新既有调用和测试注册流程，构建通过。
- [x] 1.2 覆盖网络与事件提前释放、未注册订阅和入口撤销后的句柄清理，更新架构与路线图，核对服务无全局访问。

## 2. 集成验收

- [x] 2.1 执行 tools/test.ps1（包含 tools/check.ps1）和客户端 smoke，验证登录、缓存优先、重试、退出重建；校验 change。

## Workflow follow-up

- 完成检查后同步 specs 并归档，保留本地中文约定式提交。

验收结果：tools/test.ps1 通过（包含 check，构建零警告零错误，28 个普通 .NET + 58 个 Godot 用例），客户端 smoke 通过；启动失败日志为替身预期注入。
