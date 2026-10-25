# 验证

- `powershell -ExecutionPolicy Bypass -File tools/check.ps1`：构建、资源导入及启动场景通过。
- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests`：全部检查通过，Godot xUnit 174 项通过；角色管理场景验证 64 个创角选项、首/中/末款 DT 映射、男女草稿、创建及存档贯通；UI 边界场景验证旧 1 号头型的头像回退。
- 对照 `head_composition.json` 与项目头像文件：男女各 64 个候选均有对应 DT PNG，无缺失。

界面缩略图原先使用尺寸约 20–42 像素的不同动画帧，统一排入 48×48 方框；源像素尺寸和缩放倍率不同。改为统一的 50×50 DT 原图，并在创角及圆形头像处使用最近邻采样。原素材本身的线条与色块差异仍会造成视觉差别。
