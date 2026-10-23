# Design

## Context

`NetManager` 是由 `MainController` 持有的普通 C# 实例，`GameSession` 借用它，测试通过可替换传输创建独立实例。命名跨网络、会话、UI 和测试模块，动机见 proposal.md。

## Goals / Non-Goals

保持网络对象身份、会话代次隔离和生命周期，统一类型名、文件名及当前文档术语；不引入 GameServices、单例、Autoload 或旧名称兼容包装。

## Decisions

- 原位重命名为 `Rpg.Net.NetService`，不移动命名空间。保留独立实例比静态化更符合本次纯命名范围，也保留现有测试隔离。
- 同步移动 `.cs.uid`，内容保持不变，避免 Godot 重新分配资源身份。
- 更新运行代码、现有测试、中文注释和错误信息；当前文档使用新名称，历史归档保持原样。
- 复用网络、会话与事件回归测试，通过编译发现漏改的类型引用，不新增只检查名称的测试。

## Risks / Trade-offs

- 遗漏类型或路径引用 → 搜索当前源码、测试与资源并执行完整构建、现有客户端测试和项目检查。
- UID 丢失 → 成对移动文件并确认 UID 内容一致。

## Migration Plan

同一提交中迁移所有内部调用与文档，验证通过后归档 change；回滚该提交可恢复原有名称。
