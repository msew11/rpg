# Design

## Context

现有服务器以角色三表和物品共同提交保存，世界20Hz推进、10Hz按观察者发布。角色主表保存生命魔力上限，临时移动由宿主统一速度驱动，客户端预测把速度转换为整数。

## Goals / Non-Goals

Goals：按确认方案贯通基础属性、按实体公开增量和每角色 GM 命令持久化。
Non-Goals：升级流程、装备加成、Buff、战斗公式和怪物业务。

## Decisions

- attribute.json 规定 id/name/min/max/precision/displayDecimals；role_level.json 以 level 为键保存完整六项 map。通用转换器拒绝重复或非规范数字键，十进制校验投放精度后转换为 double。
- 属性 map 在服务器加载角色时按等级重算；本人 RolePb.attributes 初始化，独立通知更新。现有上限字段仅是保存与公开投影，当前值下降裁剪、上升不补满。
- RoleWorldUpdatePb 使用可选完整分组；WorldFrame.self 每帧携带完整位置，快照携带资料及状态，远端统一 updated_roles。本人装备由物品通知拥有，不接纳延迟公开装备覆盖。
- Role 主表增加 GmMovementSpeedOverrideEnabled 和 GmMovementSpeed。默认 false/180，缺字段旧存档按默认值读取。命令复用 Gm/GmRt，RolePb 和 GmRt 各携带相同 GM 状态消息用于界面恢复。EnterWorld 请求不变。
- movespeed 复用运行时物品/保存屏障，每次成功回复前提交角色保存；投影失败保留待重试，IO 失败明确失败且保留待写。正常属性与 GM 执行速度分开，关闭保留最近调试值。断线仅停止移动，不清除已保存设置。
- 速度协议、临时移动、预测使用 double，步长先裁剪到目标距离再转引擎坐标，移除宿主全局速度注入。

## Risks / Trade-offs

- double 非精确定点 → 限定范围与精度，服务端计算并同步，拒绝非法浮点。
- 命令落盘与共同转移冲突 → 复用现有统一提交入口，不并行绕过回滚记录；捕获基线后只确认对应快照。
- 协议破坏性调整 → 双端同步构建，不维护旧格式。
- 既有测试临时配置缺新表 → 统一更新 fixture 和真实场景配置复制。
