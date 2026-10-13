# Proposal

## Why

当前调试控件常驻左侧并占据雷达上方空间，雷达又可以被面板隐藏，妨碍随时观察区块。需要可收起、可移动且按用途分组的 GM 操作入口。

## What Changes

- 增加可拖动的 GM 小按钮；点击展开面板，展开时隐藏按钮。
- 面板提供固定按钮、标题栏拖动和分组 Tab；未固定时点击面板外收起。
- 角色 Tab 提供已有素材的动作演示选择，切换角色模板时停止演示。
- 删除面板中的雷达开关，雷达始终显示在左上角。
- 游玩画面的鼠标滚轮按现有滑条步进调整视野高度。
- 调整相关 UI 回归测试及文档。

## Capabilities

### New Capabilities

- `gm-panel-ui`: GM 入口、面板拖动、固定、收起及 Tab 组织。

### Modified Capabilities

- `continuous-chunk-world`: 雷达固定显示并调整空格快捷键的验收场景。
- `player-visual-template`: 通过 GM 面板选择并演示角色动作。
- `world-interaction-events`: 调试面板不再提交雷达切换命令。

## Impact

主场景、GM 面板脚本、雷达视图、UI 测试和开发阶段文档；保留世界层调试数据与事件契约，避免改动核心和存档格式。
