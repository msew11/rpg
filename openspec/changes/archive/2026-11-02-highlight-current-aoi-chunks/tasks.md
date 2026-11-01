# Tasks

## 1. AOI 调试表现

- [x] 1.1 增加基于已接纳 `Session.Map` 的等轴 AOI 覆盖层，验证初始九块、边角裁剪、中心块区别及快照改变时更新。
- [x] 1.2 在 GM 面板接入默认关闭的“显示当前 AOI”开关，并验证绿色 chunk 线、AOI 高亮和粉色绘制区域线的层级及离开世界后的清理。
- [x] 1.3 更新架构与路线图，核对调试显示的权威数据来源及当前状态。

## 2. 集成检查

- [x] 2.1 运行 `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests`，确认构建与现有回归通过。

## Workflow follow-up

- 同步规格、归档 change，并创建中文本地提交。
