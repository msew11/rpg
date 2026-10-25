## Context

SettingService 已将宽高保存至 system.json。GameHud 显示页只有三个尺寸，MainController 启动时始终设置窗口尺寸并居中。

## Decisions

- 用数值枚举保存窗口模式，缺失或无效值回退窗口模式，旧文件保留原宽高和音量。
- Godot 的 ExclusiveFullscreen 对应“全屏”，Fullscreen 对应“无边框窗口全屏”。两种全屏均由显示器决定实际尺寸。
- 共用 DisplaySettings.Apply 执行启动和界面操作；切回窗口时应用保存的宽高并居中。
- 显示页列出 800×600 至 3840×2160 的十个常用尺寸；旧存档非预设尺寸追加为当前选项。

## Risks / Trade-offs

- 独占全屏是否可用受 Windows 显示驱动影响；Godot 负责模式切换，设置仍保存玩家选择。
