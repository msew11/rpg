# role-bag Specification

## Purpose

定义角色背包的容量、道具实例持久化、独立快照同步与图标网格呈现，确保切角、重启和旧存档兼容。

## Requirements

### Requirement: 配置与角色容量
系统 SHALL 以单例 basic.json 定义 bagColumns=8、bagRows=5，角色 BagExtraRows 初始为 0；容量 SHALL 按列数乘总行数计算并拒绝非正基础值、负增加行数与整数溢出。

#### Scenario: 基础和扩容
- **WHEN** 角色增加行数为 0 或 2
- **THEN** 背包分别有 40 或 56 格；缺失或非法基础配置阻止启动

### Requirement: 实例存档与完整快照
服务端 SHALL 使用 DataServerBag、DataServerItem 随角色原子保存实例 ID、配置 ID、格子位置、数量、独立版本和下一个实例编号。旧存档缺失字段 SHALL 为增加行数 0 和空背包。系统 SHALL 拒绝重复实例/格子、越界格子、未知配置、非法数量或编号。选角 SHALL 独立携带 BagPb，BagChangedNotice SHALL 携带角色 ID 与完整快照，移动消息 SHALL 不携带物品列表。RoleCache SHALL 保存独立且只读的 BagPbData 和 ItemPbData，先验证整份数据再替换，忽略其他角色及非较新版本，切角清空。

#### Scenario: 重启和切角
- **WHEN** 发放后重新登录或切换角色
- **THEN** 原角色物品持久保留，其他角色不收到其物品，旧消息不覆盖新会话

#### Scenario: 非法快照
- **WHEN** 当前背包收到重复格子或数量非法的较新快照
- **THEN** 拒绝整个快照并保留原有效数据，普通监听不处理该消息

### Requirement: 图标格子显示
点击背包 SHALL 按配置绘制全部格子，按 SlotIndex 显示图标及所有数量（包含 1），悬停显示配置 Name。缺图 SHALL 显示占位，关闭重开 SHALL 读取最新缓存，打开期间 SHALL 随推送刷新；窗口缩小时 SHALL 能滚动查看格子，输入不穿透世界。

#### Scenario: 发放时面板开关
- **WHEN** 背包打开或关闭期间收到发放快照
- **THEN** 缓存立即更新，打开时正确显示物品图标、数量和名称提示
