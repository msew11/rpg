# Design

## Context

源仓库中两个技能各有 `SKILL.md` 和 `agents/openai.yaml`。本项目已经使用 `.agents/skills/` 存放仓库级技能。

## Goals / Non-Goals

**Goals:** 保留两个技能的调用方式与工作树流程，同时移除 `fcfake` 和其业务目录的专用约束。

**Non-Goals:** 本次不运行派发或收集流程，不更改现有工作树或游戏功能。

## Decisions

- 复制技能目录结构到本项目 `.agents/skills/`，使技能随 main 分支分发。
- 示例改用本项目目录名，文件边界改为遵循本项目 `AGENTS.md`，避免源仓库专用规则误导后续任务。
- 本次没有游戏能力变更，跳过 delta spec。

## Risks / Trade-offs

- 技能是文字流程，无法由 Godot 检查验证其 Git 操作语义；通过文件核对与当前仓库检查确认交付状态。
