# Tasks

## 1. 上下文与缓存

- [x] 1.1 拆分三种 Context 与只读规则，移除通用上下文；编译及测试确认各自 EntityCache 类型和身份。

## 2. Handler 归属

- [x] 2.1 三类 Actor 声明所属 Handler 并在各自邮箱路由，移除通用工作委托与布尔切换；测试验证声明归属和纯内存处理。
- [x] 2.2 迁移内部操作、模拟推进和既有测试，更新架构与路线图；回归跨 Actor 副本、物品共同落地及摘要同步。

## 3. 集成验证

- [x] 3.1 运行 tools/check.ps1 -Tests 与 OpenSpec 校验，记录实际结果。

## Workflow follow-up

- 归档 change 并保留中文约定式本地提交。
