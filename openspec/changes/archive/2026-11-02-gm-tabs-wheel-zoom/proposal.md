# Proposal

## Why

滚轮目前默认改变世界相机倍率，容易误操作；GM 控件不断增加，同一列难以浏览。按用户要求将缩放限定为主动启用的调试功能，并按功能分类显示 GM。

## What Changes

- GM 增加默认关闭的“启用滚轮缩放”开关，关闭后保留当前基础倍率，后续滚轮不再改变世界。
- GM 使用“世界调试”“角色调试”“系统工具”标签页，保留全部既有功能。
- 全部 GM 设定按现有 EnvService 的 `user://.env` 方案持久化，包含所有开关、颜色、GM 倍率及血条预览；切角、重新进入与重启恢复，非法值回退，保存失败保持原状态。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `client-ui`：滚轮缩放启用条件、GM 标签分类与页内调试开关对齐、全部 GM 设定的持久化及恢复。

## Impact

修改 EnvService、GameHud 与 MainController，扩展环境配置测试及现有 C# 场景验收；同步架构和路线图。复用 .env 写入规则，不改变协议和角色存档。
