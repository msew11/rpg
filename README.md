# 2D RPG 项目

Godot 4.7.2 / GDScript 项目，首个目标平台为 Windows 桌面。目前只有一个可运行的启动场景，玩法和美术方向将在后续功能需求中确定。

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

## Windows 导出

安装与 Godot 4.7.2 匹配的 Export Templates 后，在编辑器中选用 `Windows Desktop` 预设，或在根目录运行：

```powershell
powershell -ExecutionPolicy Bypass -File tools/check.ps1 -ExportWindows
```

该命令也会启动导出的程序并确认正常退出。导出文件写入 `build/`，不提交到 Git。每个功能的开发和交付约定见 `AGENTS.md`。
