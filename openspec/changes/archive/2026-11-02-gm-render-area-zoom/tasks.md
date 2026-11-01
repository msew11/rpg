# Tasks

## 1. 世界缩放与绘制范围

- [x] 1.1 增加滚轮基础缩放、GM 叠加缩放及独立绘制范围计算；在地图分块验收中确认 GM 缩放不更换绘制块、滚轮缩放会更新绘制块。
- [x] 1.2 增加绘制区域粉色边缘覆盖层与 GM 控件，验证开关、改色和缩放后边缘对应实际绘制范围。

## 2. 文档和集成检查

- [x] 2.1 更新架构与路线图中的调试缩放说明，并运行 `powershell -ExecutionPolicy Bypass -File tools/check.ps1`，确认全套检查通过。

## Workflow follow-up

- 检查并归档 change，保留中文本地提交。
