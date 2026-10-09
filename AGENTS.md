# Agent 开发约定

- 使用 Godot 4.7.2、GDScript 和 2D 场景开发。Windows 桌面是首个验收平台。
- 以 `scenes/main.tscn` 为启动入口。按功能组织新增场景、脚本和资源；不要在准备阶段擅自确定玩法、美术风格或故事设定。
- 开始修改前确认当前 Git 状态；保留用户已有改动。每完成一个功能，运行 `powershell -ExecutionPolicy Bypass -File tools/check.ps1`，再交付可运行结果、检查结果及尚存限制。
- 遇到需要新增游戏设计决策的任务，先从用户需求中确认目标与验收标准。代码和资源只应实现已确定的功能。
- 不提交 `.godot/` 缓存和 `build/` 导出文件。功能完成后提供清晰的 Git 提交，便于回滚。
