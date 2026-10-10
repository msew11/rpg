# Godot 2D 项目框架

本项目使用 Godot 4.7.2、GDScript 和 2D 场景，以 `scenes/main.tscn` 为启动入口，首个验收平台为 Windows 桌面。当前提供可运行的等轴移动和连续区块原型。

`main` 保存不限定具体玩法的项目框架；`develop` 用于游戏方向与功能开发。当前可运行能力以代码和 `openspec/specs/` 为准。

项目方向见 [游戏愿景](docs/game-vision.md)，设计边界见 [架构文档](docs/architecture.md)，阶段顺序见 [开发路线](docs/roadmap.md)。这三份文档记录目标和建议；具体内容由 `develop` 分支维护。

## 本机准备

安装 Godot 4.7.2。检查脚本会优先使用 `GODOT_BIN` 环境变量，其次查找 PATH 中的 Godot，最后查找本机已安装的 Godot 4.7.2 控制台程序。

若安装在别处，可在 PowerShell 中设置：

```powershell
$env:GODOT_BIN = 'C:\path\to\Godot_v4.7.2-stable_win64_console.exe'
```

## 运行与检查

在项目根目录运行：

```powershell
powershell -ExecutionPolicy Bypass -File tools/check.ps1
```

脚本会检查 Godot 版本，执行无界面资源导入与主场景启动。也可以在 Godot 编辑器中打开 `project.godot`，按 F6 运行当前场景或按 F5 运行项目。

原型中使用 WASD 或方向键沿屏幕八方向移动。移动系统检查可运行：

```powershell
godot --headless --path . --script tests/movement_test.gd
godot --headless --path . --script tests/view_test.gd
```

连续区块原型使用手工配置的 4 × 4 个区块，每个区块 12 × 12 格。加载范围为玩家所在区块周围的 3 × 3 区块。有限世界的两侧相接，玩家位置与镜头连续前进；草地和泥地素材来自 Kenney Isometric Roads（CC0），这里只用于测试。视口左上角的雷达描边显示当前加载区块；黄色玩家点和朝向箭头固定在中心，区块网格随移动而平移，底部显示行走区块坐标 RAW 和回环后的 ID。按空格切换脚下的一格地块；走远再返回，或走满一次回环，改动在本次运行中保留。调试构建中按 F3 显示或隐藏当前加载区块的发光边缘；发布构建不显示该覆盖层。此原型不提供退出后的存档。

## Windows 导出

安装与 Godot 4.7.2 匹配的 Export Templates 后，在编辑器中选用 `Windows Desktop` 预设，或在根目录运行：

```powershell
powershell -ExecutionPolicy Bypass -File tools/check.ps1 -ExportWindows
```

该命令也会启动导出的程序并确认正常退出。导出文件写入 `build/`，不提交到 Git。每个功能的开发和交付约定见 `AGENTS.md`。

## 用 OpenSpec 记录目标

本项目已初始化 OpenSpec 的 Codex 集成。每个目标对应 `openspec/changes/` 中的一项 change；完成并归档后，确认的需求会进入 `openspec/specs/`。尚未确定的游戏设计不写成既定要求。

1. 描述一个具体目标及其验收标准，使用 Codex 的 `$openspec-propose` 创建提案与任务。若命令尚未出现，重启 Codex 后再试；也可以先运行 `openspec new change <目标名>` 建立记录。
2. 检查并确认提案中的范围、需求场景和待定问题，再使用 `$openspec-apply-change` 实现。
3. 每完成一个功能，运行 `powershell -ExecutionPolicy Bypass -File tools/check.ps1`，确认验收结果后使用 `$openspec-archive-change` 归档。

OpenSpec CLI 可用 `npm install -g @fission-ai/openspec@latest` 安装；项目中的 `.agents/skills/` 是供 Codex 使用的集成文件。升级 CLI 后，在项目根目录运行 `openspec update` 同步它们。
