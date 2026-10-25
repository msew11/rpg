# Tasks

## 1. 地图音乐配置

- [x] 1.1 导入 M001 与 M015a，给地图配置新增可选 BGM 编号并将地图 1 设为 M015a；验证资源可导入、配置读取及非法编号诊断。
- [x] 1.2 更新 client-config-map 规格和架构中地图配置说明，核对字段及未配置行为。

## 2. 页面与地图播放

- [x] 2.1 新增循环 BGM 播放器，接入登录、选角、地图进入及返回流程；以场景测试验证曲目切换和 BGM 总线，并核对同曲早退逻辑。
- [x] 2.2 更新 client-ui 规格、架构和路线图，核对实际播放时机与资源编号。

## 3. 集成检查

- [x] 3.1 运行 `powershell -ExecutionPolicy Bypass -File tools/check.ps1` 及扩展测试，确认 C# 构建、资源导入和场景检查通过。

## Workflow follow-up

- 检查通过后同步规格并归档 change。
