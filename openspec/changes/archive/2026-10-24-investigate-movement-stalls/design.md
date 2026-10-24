## Context

截图来自 Rider .NET Monitoring，只有 Godot 编辑器存活，未留下可分析的 dotTrace 快照。不能把 UI freeze 时长直接视为游戏帧时间。当前 MainController 固定帧推进网络、世界、角色与相机；MapView 按可见范围增删静态分块；动画在首次使用时同步加载；MockServer 确认移动时同步原子存档。

## Goals / Non-Goals

目标：以独立存档启动真实 MainController，自动登录、选角和连续点击移动；区分冷启动和稳态，输出帧时间分位数、最大值、引擎 CPU 时间及托管分配。根据证据优化并验证行为回归。

不涉及：改变游戏设计、降低画质、修改用户存档、禁用监控告警。

## Decisions

- 诊断场景复用 DoubleMainController 与现有 GameServicesTestFactory，不为正式服务加入测量逻辑。
- 性能阈值不作为硬件无关的 CI 断言；自动验收检查登录、角色移动、分块覆盖和资源表现。
- 使用实际 Windows 图形渲染测量，headless 仅作功能回归，具体优化和前后数据记录在 validation.md。

## Risks / Trade-offs

后台应用、VSync、驱动与调试器会影响帧时间；同设置对比且说明无法重建原截图调试环境。
