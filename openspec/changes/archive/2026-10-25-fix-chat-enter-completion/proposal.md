## Why

候选框显示时按回车仍然发送输入，会误执行未完成的命令。用户期望回车确认当前候选。

## What Changes

- 候选框可见时，主键盘回车和小键盘回车补全当前选项并消费按键。
- 候选框隐藏时保留回车提交，保留点击与 Tab 补全。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `gm-item`: 明确回车优先确认候选。

## Impact

ChatControl 的候选按键处理、背包 GM 场景回归以及相关架构说明。
