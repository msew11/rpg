## Context

已有按角色 JSON 的原子保存、Cache 优先事件分发、单例配置缓存与道具图集。背包数据归 RoleCache，服务端模型与客户端模型隔离。

## Goals / Non-Goals

**Goals:** 通过 GM 发放验证配置、存档、协议、缓存与可见背包的完整链路。

**Non-Goals:** 普通聊天、移动、拆分、使用、丢弃、整理、装备属性与真实远程 GM 权限系统。

## Decisions

- basic.json 使用 BasicCfgCache，容量由列数乘基础行与角色增加行之和计算，检查正数、非负数及溢出。
- DataServerBag 嵌入角色文件；Items 使用不可变实例，发放在副本上计算，保存成功才替换当前角色。独立文件会引入跨文件事务，当前不采用。
- NetService 增加 Config 构造依赖，将容量与 Item 定义复制成服务端独立只读配置，MockServer 不引用 Godot 或客户端配置类型。
- ItemPb 包含 instance_id、item_id、slot_index、count。BagPb 包含 revision 和 repeated items，选角独立携带背包；角色状态增加 bag_extra_rows。背包 Notice 全量替换，不在移动消息重复传输。
- Gm/GmRt 传递角色 ID、原命令与反馈。命令 item 按空白分隔；数量省略为 1。先按格子补堆叠，再按空格建实例；失败不产生版本或编号变化。
- RoleCache 原子转换完整选角数据与背包快照；非当前角色和旧版本推送忽略。普通 UI 阶段只读缓存。
- ItemCfg.IconAssetId 可选，缺省为空并显示占位；七件正式测试装备明确绑定已有素材。独立背包控件读取素材索引，不依赖图鉴窗口。
- 聊天区放在 HUD 底部左侧，输入与回显独立于互斥弹窗；背包面板使用可滚动 8 列网格，小窗口保持在视口内。

## Risks / Trade-offs

- 全量快照成本随物品数增长 → 当前容量 40 格，后续再引入增量协议。
- 非法存档或配置变动导致实例超容量、未知配置或超堆叠 → 拒绝加载或同步并保留既有有效数据，不静默删除物品。
- 图标映射依赖候选素材 → 显式记录素材键，缺图有占位，不从重复来源 ID 推断。

## Migration Plan

旧角色默认空背包、增加行数 0；不补发道具。新增字段使用未占用 protobuf 编号。完成后运行项目检查、完整用例及真实节点验收，更新文档并归档。

## Open Questions

无。本轮只创建基础实例，不定义装备属性。
