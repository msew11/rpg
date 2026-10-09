# Agent 开发约定

- 使用 Godot 4.7.2、GDScript 和 2D 场景开发。Windows 桌面是首个验收平台。
- 以 `scenes/main.tscn` 为启动入口。按功能组织新增场景、脚本和资源；不要在准备阶段擅自确定玩法、美术风格或故事设定。
- 开始修改前确认当前 Git 状态；保留用户已有改动。每完成一个功能，运行 `powershell -ExecutionPolicy Bypass -File tools/check.ps1`，再交付可运行结果、检查结果及尚存限制。
- 遇到需要新增游戏设计决策的任务，先从用户需求中确认目标与验收标准。代码和资源只应实现已确定的功能。
- 每个新目标使用 OpenSpec 记录为独立 change。先确认目标、范围和可验证的验收场景，再编写设计与任务并实现；完成检查后归档 change。未确认的玩法、美术和故事决定保持待定。
- 开始新 change 前阅读 `docs/game-vision.md`、`docs/architecture.md`、`docs/roadmap.md` 和相关 `openspec/specs/`。愿景记录长期目标，架构记录设计边界，路线图记录阶段顺序；`openspec/specs/` 与代码记录当前能力，不把未来目标写成已实现要求。
- change 若改变产品方向、模块边界或阶段顺序，须在同一次交付中更新相应长期文档；完成时核对文档与代码、specs 一致，并更新路线图状态。未定事项先标为待定，不由 agent 擅自定案。
- 不提交 `.godot/` 缓存和 `build/` 导出文件。功能完成后提供清晰的 Git 提交，便于回滚。
- 所有新 Git 提交遵循约定式提交（Conventional Commits）：标题格式为 `<type>[(<scope>)][!]: <description>`，其中范围和 `!` 可选，例如 `feat(combat): add turn order`、`fix: correct scene loading`、`docs: clarify setup steps`。常用类型包括 `feat`、`fix`、`docs`、`refactor`、`test`、`chore`；有破坏性变更时在类型或范围后加 `!`，并在正文脚注写明 `BREAKING CHANGE: <说明>`。
