# Tasks

## 1. 缓存职责拆分

- [x] 1.1 将三个表缓存平级组合到 RepoEntityCache，迁移仓库与候选投影；更新实体加载/回滚测试和架构、路线图，验证 .NET 测试通过且 JSON 结构不变。

## 2. 集成验收

- [x] 2.1 运行 tools/check.ps1 -Tests、OpenSpec 严格校验及 git diff --check，确认全部场景通过并保留中文本地提交。

## Workflow follow-up

- 检查通过后归档 change。

## Verification

- tools/check.ps1 -Tests：构建零警告、零错误，256 项 .NET 测试、220 项 Godot 测试和全部 13 项真实场景验收通过。
- 新回归验证平级表缓存重组后完整属性、穿戴、实例编号和内存临时序号保持；现有懒加载、四表回滚和迁移回归通过。
- OpenSpec 严格校验及 git diff --check 通过。
