## Why

当前启动流程读取已保存分辨率后直接调整窗口大小，没有重新居中，窗口可能偏离显示器中心。用户希望游戏打开后位于显示器中央，需要在最终启动尺寸应用后统一处理位置。

## What Changes

- 启动时应用保存的窗口分辨率后，调用 Godot 窗口居中接口。
- 居中目标为窗口当前所在显示器；使用 Godot 内置支持处理多显示器及嵌入窗口。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `client-settings`: 增加启动窗口居中行为。

## Impact

MainController 的启动设置应用及路线图。不增加设置字段。
