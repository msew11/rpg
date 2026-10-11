## Context

核心 C# 项目已启用 nullable。`EcsWorld` 的若干参数目前写成 `?` 并显式处理空值，但调用方可以保证组件与系统参数不为空；缺失组件的返回值仍需保持可空。

## Goals / Non-Goals

**Goals:** 使 API 签名准确表达调用前置条件，并保留现有有效输入与缺失资源行为。

**Non-Goals:** 不改变 ECS 数据结构、返回值可空性、具体玩法调用方或仓库查找语义。

## Decisions

- 组件实例、组件 `Type`、系统实例入参移除 `?`，移除对应的 null 分支和 null-forgiving 运算符。
- `GetComponent` 的返回值保留 `?`，因为组件不存在是正常场景；`DataRepoCache.GetRepo` 的返回值也保留 `?`。
- `Query(params Type[])` 本身已声明非空，删除冗余的数组空值断言。

## Risks / Trade-offs

- [旧调用方显式传入 null] → C# nullable 分析会提示调用问题；调用方应修正为有效实例或类型。
