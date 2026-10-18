# 客户端架构

## 数据和生命周期

`RepoGameConfig` 在启动时递归扫描 `res://config`，以 `res://schemas` 中的 Draft 2020-12 JSON Schema 校验，组合为只读的 `DataGameConfig`。首期只有地图配置：尺寸、瓦片规格、默认地块及稀疏覆盖格。配置与程序同包发布；未知、重复或无效文件阻止启动。

模拟状态源处理演示登录和移动确认。登录返回角色列表；选角后 `GameSession` 持有该角色的 `DataCache`。缓存保存最新权威状态并发出更新通知。以后真实网络适配器应在会话边界替换模拟状态源，不把传输细节交给世界或 UI。

本地设置与上述两类数据分离：系统设置保存在 `user://settings/system.json`；角色设置按 `role_id` 保存在 `user://settings/roles/`。设置仅控制本机显示、声音和 HUD 布局，不作为服务器权威数据。

## 输入、世界与表现

独立的 `InputSystem` 把未被 UI 消费的 Godot 输入转换为世界 Action，也为今后的 UI 输入路由留出位置。`WorldManager` 是 ECS 的唯一外部写入入口：输入 Action、缓存状态同步 Action 排队后由 Godot 固定帧处理。初始化顺序为地图世界实体、角色实体。轻量 ECS 由整数实体 ID、组件仓库和固定顺序系统组成；首期移动系统按直线推进并接受权威位置修正。

`MapManager` 内聚地图坐标、边界与可见格计算。地图表现只绘制视口可见地块，不创建百万个瓦片节点。角色表现只读取 ECS 位置；HUD、小地图和系统面板放在独立 `CanvasLayer`，不受世界摄像机变换影响，从 `DataGameConfig`、`DataCache` 与本地设置读取各自所需的数据。

```text
JSON 配置 -> RepoGameConfig -> DataGameConfig -> 地图管理 / UI
模拟状态源 -> GameSession -> DataCache -> 状态同步 Action -> WorldManager -> ECS -> 世界表现
Godot 输入 -> InputSystem -> 移动 Action ------------------------^
本地设置 ----------------------------------------------------> UI
```

## 当前限制

演示认证、状态确认和地图配色仅用于验证客户端链路；尚无真实网络、寻路、技能效果、背包操作或正式美术资源。模拟服务端位置与客户端运动存在固定小偏差，用于检查校正路径。
