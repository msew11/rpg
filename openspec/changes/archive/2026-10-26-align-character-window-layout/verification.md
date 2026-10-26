# 检查记录

- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests` 通过：209 个普通 .NET 测试、206 个 Godot xUnit 测试及全部场景验收通过，编译零警告、零错误。
- 装备场景新增实际矩形检查：48×48 背包格与十一装备槽等大；七件装备逐一核对图标区域、素材大小、等比居中与采样一致；装备槽没有数量标签；主副手与鞋子上下边缘对齐且位于预览下方；正常窗口右边距与左边距仅相差布局余量。
- 旋转按钮实际为 28×28，图标 18×18，显式居中且位于手部槽上方；八方向旋转、普通点击、Ctrl 自动穿脱、满包拒绝及输入隔离继续通过。
- 窗口边界场景检查至少八处普通窗口标题均与背包字号一致，保留跨分辨率、滚动与真实关闭验收；角色管理及图鉴场景通过。
- `godot-mono --path . res://tests/equipment_smoke.tscn -- --capture-equipment` 图形模式通过。人工核对正常窗口和 480×270 小窗口，截图为 `build/equipment-window.png`、`build/equipment-window-small.png`；裁剪展示为 `build/character-layout-preview.png`，均不提交。
- `openspec validate align-character-window-layout --strict`、28 个主规格校验及 `git diff --check` 通过。主规格保留其他既有 Purpose/长度警告。

## 行为边界

仅更新布局、标题和格子显示。装备协议、存档、槽位映射与人物原始比例保持既有契约，选角及创角继续显示其原有头像和比例。普通窗口标题统一，原生确认对话框沿用引擎行为。
