# Proposal

## Why

审查页中副手测试飞刀在朝右、右下跑步时像贴在手背上：镜像前已切换到右手身体挂点，却仍使用左手飞刀原画。需要让飞刀原画、握点与源手别一致，并同步原路径审查包。

## What Changes

- 导入原始右手飞刀各动作，与现有左手飞刀分别保留连接点、帧与层次。
- 按用户补充要求，审查全部七件装备的同类错手问题；自动发现并导入已有同系列双手原画，保留单手来源的明确回退记录。
- 镜像时选择对应源手别资源，重点验收男女朝右、右下跑步全部帧，其余八方向与动作回归。
- 重新实绘并同步更新 build/actor-review-2026-10-26/index.html 及图片、清单与压缩包。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `client-actor-presentation`：补充飞刀源左右手持握与镜像跑步场景。

## Impact

装备导入器、DataActorWeapon、AnimationService、飞刀资源、动画冒烟验收及审查导出。装备槽规则保持现状，飞刀仍为副手。
