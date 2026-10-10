# Proposal

## Why

本项目需要复用 `git-dispatch` 和 `git-collect` 工作树流程，作为仓库内的框架级技能供后续开发使用。

## What Changes

- 将两个技能及其界面元数据迁入 `.agents/skills/`。
- 将源仓库专用示例和目录限制改为本项目适用的说明，保留派发、收集、提交和工作树安全检查的流程。

## Capabilities

### New Capabilities

无。本次增加代理开发流程技能，不改变游戏运行能力。

### Modified Capabilities

无。

## Impact

只影响开发代理的 Git 工作流；不改变场景、脚本、资源或游戏行为。
