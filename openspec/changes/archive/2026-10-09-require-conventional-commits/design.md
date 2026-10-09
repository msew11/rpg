# Design

## Context

现有 `AGENTS.md` 是代理开发与提交约定的入口，当前没有提交信息校验工具。参见 `proposal.md`。

## Goals / Non-Goals

**Goals:** 为后续提交提供明确、可照用的标题格式。

**Non-Goals:** 本次不安装 Git hook 或增加依赖。

## Decisions

- 在 `AGENTS.md` 的现有提交规则后追加 Conventional Commits 规则。这样所有代理能在执行任务时读到约束；相比新增 hook，无需在每个克隆中安装额外配置。
- 将范围与破坏性标记写为可选，并列出常用类型和示例，避免格式说明产生歧义。

## Risks / Trade-offs

- 文档规则无法阻止手工提交不合规信息 → 后续若需要机械校验，可单独引入提交信息 hook 或 CI 检查。
