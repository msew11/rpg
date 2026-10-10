# Agent 开发约定

- 分支文档归属：`main` 维护 `AGENTS.md` 和 `README.md`。这两个文件只在 `main` 修改；`develop` 通过合并 `main` 获取更新，不写入玩法、原型或开发进度细节，合并后保持与 `main` 内容一致。
- `main` 保留空白的 `docs/architecture.md`、`docs/game-vision.md`、`docs/roadmap.md` 作为占位文件，不在其中写入内容；`develop` 独立维护这三个文件的正文。合并 `main` 到 `develop` 时保留 `develop` 的版本，不用空白占位文件覆盖正文。玩法、原型和阶段进度写在开发分支的这些文档、相应 specs 或功能文档中。
- `scripts/core/` 由 `main` 维护；`develop` 通过合并 `main` 获取其更新，不直接新增、修改或删除目录内文件，并保持目录内容与最新 `main` 一致。
- 可以直接在本地 `main` 和 `develop` 开发并提交。完成工作后保留本地提交；只有用户明确要求“推送”时，才将所指定本地分支相对其远程目标分支累计的全部未推送提交，一并推送到同一条远程工作分支。未经明确要求，不执行 `git push`、创建远程分支或 PR；不要为了日常开发预先创建本地工作分支。两个本地分支的提交分别处理，不混入同一条远程工作分支。
- 远程 `main` 和 `develop` 禁止直接推送。需要更新任一远程目标分支时，以承载该目标分支本地提交的远程工作分支创建对应 PR，通过 PR 合并。从 `main` 同步到 `develop` 也须通过以 `develop` 为目标的 PR。
- 使用 Godot 4.7.2、GDScript 和 2D 场景开发。Windows 桌面是首个验收平台。
- 以 `scenes/main.tscn` 为启动入口。按功能组织新增场景、脚本和资源；不要在准备阶段擅自确定玩法、美术风格或故事设定。
- 新增或修改类和方法时，为其添加简明的中文注释，说明职责或行为；避免复述代码或写冗长注释。
- 开始修改前确认当前 Git 状态；保留用户已有改动。每完成一个功能，运行 `powershell -ExecutionPolicy Bypass -File tools/check.ps1`，再交付可运行结果、检查结果及尚存限制。
- 遇到需要新增游戏设计决策的任务，先从用户需求中确认目标与验收标准。代码和资源只应实现已确定的功能。
- 每个新目标使用 OpenSpec 记录为独立 change。先确认目标、范围和可验证的验收场景，再编写设计与任务并实现；完成检查后归档 change。未确认的玩法、美术和故事决定保持待定。
- 在 `develop` 开始新 change 前阅读 `docs/game-vision.md`、`docs/architecture.md`、`docs/roadmap.md` 和相关 `openspec/specs/`；在 `main` 阅读相关 `openspec/specs/`。愿景记录长期目标，架构记录设计边界，路线图记录阶段顺序；`openspec/specs/` 与代码记录当前能力，不把未来目标写成已实现要求。
- `develop` 上的 change 若改变产品方向、模块边界或阶段顺序，须在同一次交付中更新相应长期文档；完成时核对文档与代码、specs 一致，并更新路线图状态。未定事项先标为待定，不由 agent 擅自定案。
- 不提交 `.godot/` 缓存和 `build/` 导出文件。功能完成后提供清晰的 Git 提交，便于回滚。
- 所有新 Git 提交遵循约定式提交（Conventional Commits）：标题格式为 `<type>[(<scope>)][!]: <description>`，其中范围和 `!` 可选，例如 `feat(combat): add turn order`、`fix: correct scene loading`、`docs: clarify setup steps`。常用类型包括 `feat`、`fix`、`docs`、`refactor`、`test`、`chore`；有破坏性变更时在类型或范围后加 `!`，并在正文脚注写明 `BREAKING CHANGE: <说明>`。
