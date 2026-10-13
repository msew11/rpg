# Proposal

## Why

当前相机视野较窄，调试者难以直接观察活跃区块外的空白区域。点击 Debug 开关后，空格键还会触发该开关，与地块切换快捷键冲突。

## What Changes

- Debug 面板增加视野高度滑条，可在游玩中扩大相机可见范围。
- 空格键切换地块时不再同时激活之前点击的 Debug 开关。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `continuous-chunk-world`：调试面板可调整视野高度，并让地块快捷键与开关互不干扰。

## Impact

修改主场景、Debug 面板和输入处理；补充区块视图回归测试。相机调试设置不写入存档。
