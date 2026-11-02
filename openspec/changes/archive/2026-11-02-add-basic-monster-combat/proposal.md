# Proposal

## Why
现有武器只有持握外观、怪物只有静态展示，缺少可供玩家操作和多人观察的战斗反馈，也无法验证武器数值的实际作用。按已确认方案建立空手和武器普通攻击闭环，统一走近、服务器时序、随机点伤、伤害跳字与怪物生命保存，为后续战斗扩展提供可验证的基础。

## What Changes
- **BREAKING** 停用点伤编号4，新增整数上下限、范围、每秒攻击次数。
- 装备投放、空手等级投放和怪物完整属性、血量持久化。
- 左键走近与自动攻击、前摇取消、权威命中及即时多人跳字。

## Capabilities
### New Capabilities
- `basic-monster-combat`: 普攻时序、结算、即时事件和死亡保存。
### Modified Capabilities
- `entity-attributes`: 完整属性扩展和装备计算。
- `equipment-config`: 武器基础数值。
- `monster-selection`: 左键攻击、右键选择与取消。

## Impact
配置、protobuf、Role/World Actor、会话缓存、ECS和Godot表现；不新增服务或依赖，不实现AI、掉落或复活。
