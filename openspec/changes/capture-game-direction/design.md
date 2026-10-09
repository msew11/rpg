# Design

## Context

项目目前只有启动场景与工具链；`AGENTS.md` 已规定每个新目标使用 OpenSpec，但还没有长期方向文档或游戏能力 spec。参见 `proposal.md`。

## Goals / Non-Goals

**Goals:** 让 agent 能区分已确认方向、建议的架构约束、实际已实现行为和未决问题；为后续 change 提供一致的文档入口。

**Non-Goals:** 不选择 ECS 插件、地图生产方式、服务端技术栈，也不在本次实现游戏功能。

## Decisions

- `docs/game-vision.md` 只记录产品目标与范围，`docs/architecture.md` 记录技术边界及其状态，`docs/roadmap.md` 记录顺序与验收出口。相比把全部内容写进一个文件，三者有不同的更新触发条件。
- OpenSpec change 管具体增量；`openspec/specs/` 管已实现能力。项目愿景和未来 MMO 目标不写成现有系统的 spec，避免误导验收。
- `AGENTS.md` 作为 agent 入口，要求读取长期文档，并在相关 change 中同步更新。相比仅靠文档自述，这能把更新动作放进既有开发流程。
- 路线图写阶段目标和依赖，不承诺日期。当前没有内容规模、团队资源或 MMO 目标人数，日期会制造无依据的计划。

## Risks / Trade-offs

- 目标架构可能被误认为已实现 → 文档明确标记“当前状态”“设计约束”和“待决定”。
- 长期文档可能滞后 → 每个 change 的验收包含检查相关文档，归档时更新路线图。
