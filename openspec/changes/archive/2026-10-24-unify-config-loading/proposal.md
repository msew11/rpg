## Why

ConfigService 为脱离 Godot 的单元测试维护了 System.IO 加载分支，增加正式代码路径。按用户要求让测试适配正式运行环境，配置只保留 Godot 资源读取。

## What Changes

- 移除配置构造中的路径判断和独立磁盘 Load，统一通过 Godot API 扫描及读取，再使用现有校验。
- 配置与整组服务测试在 Godot headless 内执行，复用现有 xUnit 用例和运行器；纯地图测试直接提供数据。
- 增加统一测试命令，分别运行普通 .NET 用例和依赖 Godot 的用例，不为测试修改业务入口。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `client-config-map`：配置统一使用 Godot 资源读取，测试走相同加载流程。

## Impact

ConfigService、测试环境与执行工具、架构及路线图；不改变配置格式、服务初始化入口或玩法。
