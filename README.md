# Godot 2D 项目框架

本项目使用 Godot 4.7.2 .NET 版和 2D 场景，以 `scenes/main.tscn` 为启动入口，首个验收平台为 Windows 桌面。核心框架使用 C#；其他尚未迁移的脚本暂用 GDScript。

`main` 保存不限定具体玩法的项目框架；`develop` 用于游戏方向与功能开发。当前可运行能力以代码和 `openspec/specs/` 为准。

`main` 保留空白的游戏愿景、架构和路线文档作为占位文件；正文只在 `develop` 分支维护。

## 分支更新

可以直接在本地 `main` 和 `develop` 开发并提交。完成工作后先保留本地提交；用户明确要求“推送”时，才把指定本地分支累计的全部未推送提交一并推送到一条远程工作分支。两个目标分支分别处理。远程 `main` 和 `develop` 禁止直接推送；需要更新它们时，从对应远程工作分支创建指向目标分支的 Pull Request（PR），通过 PR 合并。从 `main` 同步到 `develop` 也须通过以 `develop` 为目标的 PR。各分支负责的文件见 `AGENTS.md`。

## 本机准备

安装 Godot 4.7.2 .NET 版和 .NET 8 SDK。保留仓库中的 `rpg.sln`：Godot .NET 导出会查找它，在 Rider 中也应打开该解决方案以加载 C# 项目。检查脚本会优先使用 `GODOT_BIN` 环境变量，其次查找 PATH 中的 Godot，最后查找本机已安装的 Godot 4.7.2 .NET 控制台程序；普通版 Godot 无法运行核心 C# 脚本。

若安装在别处，可在 PowerShell 中设置：

```powershell
$env:GODOT_BIN = 'C:\path\to\Godot_v4.7.2-stable_mono_win64_console.exe'
```

## 运行与检查

在项目根目录运行：

```powershell
powershell -ExecutionPolicy Bypass -File tools/check.ps1
```

脚本会检查 Godot .NET 版本，构建 C# 项目，执行无界面资源导入、主场景启动和核心契约测试。也可以在 Godot .NET 编辑器中打开 `project.godot`，按 F6 运行当前场景或按 F5 运行项目。

## Windows 导出

先在 Godot .NET 编辑器的 **Editor > Manage Export Templates** 中安装 **4.7.2 Mono Export Templates**（不能使用标准版模板）。然后选用 `Windows Desktop` 预设，或在根目录运行：

```powershell
powershell -ExecutionPolicy Bypass -File tools/check.ps1 -ExportWindows
```

该命令也会启动导出的程序并确认正常退出，然后将 `build/windows/` 中的 `rpg.exe` 和 .NET 运行时依赖打包为 `build/rpg-windows.zip`。分发压缩包时需完整解压后运行 `rpg.exe`，保留其中的相对目录结构。导出文件不提交到 Git。

在 Rider 中打开 `rpg.sln` 后，可从右上角的运行配置列表选择 **Export Windows ZIP**，点击运行。该共享配置执行上面的检查与导出命令；需要 Rider 的 Shell Script 插件（通常随 Rider 安装）。每个功能的开发和交付约定见 `AGENTS.md`。

## 用 OpenSpec 记录目标

本项目已初始化 OpenSpec 的 Codex 集成。每个目标对应 `openspec/changes/` 中的一项 change；完成并归档后，确认的需求会进入 `openspec/specs/`。尚未确定的游戏设计不写成既定要求。

1. 描述一个具体目标及其验收标准，使用 Codex 的 `$openspec-propose` 创建提案与任务。若命令尚未出现，重启 Codex 后再试；也可以先运行 `openspec new change <目标名>` 建立记录。
2. 检查并确认提案中的范围、需求场景和待定问题，再使用 `$openspec-apply-change` 实现。
3. 每完成一个功能，运行 `powershell -ExecutionPolicy Bypass -File tools/check.ps1`，确认验收结果后使用 `$openspec-archive-change` 归档。

OpenSpec CLI 可用 `npm install -g @fission-ai/openspec@latest` 安装；项目中的 `.agents/skills/` 是供 Codex 使用的集成文件。升级 CLI 后，在项目根目录运行 `openspec update` 同步它们。
