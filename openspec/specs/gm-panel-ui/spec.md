# gm-panel-ui Specification

## Purpose
定义游玩时 GM 工具的可见入口、展开与固定状态、拖动位置和功能分组，使调试操作可用且不持续遮挡游戏画面。

## Requirements

### Requirement: GM 面板可收起与固定
系统 SHALL 以可拖动的 GM 小按钮作为面板入口；面板打开时 SHALL 隐藏小按钮，未固定时点击面板外 SHALL 收起并恢复小按钮。

#### Scenario: 打开与收起
- **WHEN** 调试者点击 GM 小按钮，再点击面板外
- **THEN** 面板先打开且小按钮隐藏，随后面板收起且小按钮恢复

#### Scenario: 固定面板
- **WHEN** 调试者点击面板左上角钉住按钮，再点击面板外
- **THEN** 面板保持打开；取消固定后点击面板外，面板收起

### Requirement: GM 控件可拖动
系统 SHALL 允许拖动 GM 小按钮及面板标题栏，并使控件保持在可见视口内。

#### Scenario: 移动控件
- **WHEN** 调试者拖动小按钮或面板标题栏
- **THEN** 对应控件移动到新位置，拖动小按钮不打开面板

### Requirement: GM 功能分组
系统 SHALL 在面板中用 Tab 按世界、角色和存档划分已有调试功能，切换 Tab SHALL 保留当前选择与调试状态。

#### Scenario: 切换分组
- **WHEN** 调试者在三个 Tab 间切换
- **THEN** 对应功能控件可用，已设定的滑条、边缘线和角色模板保持原值
