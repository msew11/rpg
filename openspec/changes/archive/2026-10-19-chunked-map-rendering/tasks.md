# Tasks

## 1. 地图分块表现

- [x] 1.1 实现 8×8 静态瓦片分块绘制，构建后只绘制一次；通过场景检查确认分块内容与现有等轴地块一致。
- [x] 1.2 让地图视图仅保留当前视口范围的分块并复用交集，覆盖相机移动、窗口变化和边缘；通过场景测试验证分块增删及复用。
- [x] 1.3 更新架构说明与路线图，核对文档与分块行为一致。

## 2. 集成验收

- [x] 2.1 运行单元测试、Godot 场景测试及 `tools/check.ps1`，全部通过。
- [x] 2.2 在 1280×720 的 Windows Player 中复测静止与连续移动，记录稳定 FPS 和跨分块帧耗时，确认稳定移动达到至少 60 FPS。

## 验收记录

- `dotnet test tests/ClientTests/ClientTests.csproj`：7/7 通过。
- `client_smoke.tscn`、`map_chunk_smoke.tscn`：通过；后者验证静止复用、跨界增删、窗口变化与地图边缘。
- `powershell -ExecutionPolicy Bypass -File tools/check.ps1`：通过。
- Windows Player、1280×720、RTX 5060、D3D12：静止约 100 FPS，连续单向移动约 100 FPS，跨界最大帧间隔约 20 ms；往返移动复测约 100 FPS，跨界最大约 36 ms。首次往返运行出现一次 96 ms 尖峰，后续两次未重现。

## Workflow follow-up

- 检查并同步规格后归档 change，提交清晰的本地 Conventional Commit。
