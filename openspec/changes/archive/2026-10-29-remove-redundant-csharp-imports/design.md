# Design

## Context

见 proposal.md。DropItemHandler 的两条 using 与请求类型限定已由 Rider 确认为冗余；同类写法还出现在其他 Handler。protobuf 生成目录由构建重建，必须保持生成结果。

## Goals / Non-Goals

目标：仅移除语义分析确认不会改变绑定结果的引用和名称限定。
不涉及：未调用成员删除、Handler 静态化、业务重构或整体格式化。

## Decisions

- 使用 Roslyn 的 IDE0005、IDE0001 定向整理手写 C#，避免按字符串猜测引用是否使用；复查 Rider 报告及差异。IDE0002 同时包含继承成员访问等其他风格调整，本次不批量应用。
- 排除 src/Net/Message 和构建产物；保留解决类型冲突必需的限定。
- 复用既有测试验证行为，无需为纯语法删减添加重复测试。长期文档和路线图的阶段不变。

## Risks / Trade-offs

- 分析器覆盖存在差异 → 结合 Rider 对截图文件及同类 Handler 复查。
- 名称解析或链接编译受影响 → 检查解决方案项目并执行 tools/check.ps1 -Tests。

## Migration Plan

本地 develop 单次提交；如需回滚可撤销该提交，无数据迁移。
