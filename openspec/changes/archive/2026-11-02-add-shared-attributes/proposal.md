# Proposal

## Why

人物需要可复用于怪物的统一属性，并以等级表投放基础数值。当前公开更新按位置和装备分别组织，GM 移速又由宿主统一覆盖，无法独立保存每个角色的调试状态。

## What Changes

- 添加属性约束表与等级完整基础属性表，使用数字键 JSON map 和 double 实际值。
- 本人属性通过进入响应和独立通知同步；公开变化统一按实体组帧。
- **BREAKING**：替换位置/装备专用更新消息，移速改为 double，客户端和服务器同步更新。
- movespeed 命令及 GM 勾选控件按角色保存开关和速度，每次成功回复前落盘；EnterWorld 请求保持不变。

## Capabilities

### New Capabilities
- `entity-attributes`：通用属性约束、等级投放、本人属性同步和整数/小数显示。
- `gm-movement`：按角色持久化的 GM 移速覆盖及面板、命令同步。

### Modified Capabilities
- `client-ui`：本地 GM 显示设置与按角色移速保存分开。
- `role-equipment`：公开装备复用实体更新根节点。
- `server-runtime`：GM 调速涉及公开投影，归入异步编排。
- `server-movement`：公开更新按实体合并，移速支持小数。
- `client-ecs-world`：GM 移速改由服务器角色存档驱动，不再读取本地环境速度。

## Impact

涉及 proto、配置与 Schema、角色存储、服务端移动与 AOI、客户端缓存、GM 面板和回归测试。无新外部依赖，不增加玩法公式。
