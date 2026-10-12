# Proposal

## Why

当前玩家无法从游戏界面找到本地存档文件夹，调试存档内容时需要自行查找 Godot 的用户数据目录；导出运行时该位置也可能与项目目录不同。

## What Changes

- Debug 面板显示当前正式存档槽位使用的绝对目录路径，并允许选中复制。
- 显示值从当前 `SaveSlots` 实例取得，自定义存档目录也能正确显示。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `start-menu-and-save-slots`：增加在 Debug 面板查看存档保存目录的可观察行为。

## Impact

影响主场景、Debug 面板、主入口绑定和视图测试；不改变存档文件格式或写入位置。
