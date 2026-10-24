## ADDED Requirements

### Requirement: 业务事件订阅边界
业务事件订阅 SHALL 只出现在 CacheBase 实现类和 Godot UI 类中；CacheBase SHALL 提供缓存阶段订阅与句柄托管机制。业务服务 SHALL 不注册协议监听、持有订阅句柄或通过回调包装间接订阅。网络服务 SHALL 继续负责发布解码消息和清理待处理消息。缓存 SHALL 在缓存阶段完成数据校验与更新，Godot UI SHALL 在普通阶段读取缓存并显式调用服务完成世界初始化或权威同步；服务 SHALL 继续拥有世界状态并通过 Action 写入 ECS。

#### Scenario: 首次登录缓存优先
- **WHEN** UI 先于登录缓存注册，随后发起登录并收到成功响应
- **THEN** 登录缓存先完成摘要校验与更新，UI 读取已认证状态；SessionService 没有登录订阅

#### Scenario: UI 协调世界初始化
- **WHEN** 当前角色选角响应成功且角色缓存初始化完成
- **THEN** Godot UI 显式调用世界服务建立地图、角色实体和角色设置后再创建表现，WorldService 没有选角订阅

#### Scenario: UI 协调权威同步
- **WHEN** 当前角色收到成功移动响应或有效状态推送
- **THEN** 缓存先更新快照，Godot UI 调用世界服务提交同步 Action，固定帧处理修正；HUD 是否存在不影响主控制器同步

#### Scenario: 非法缓存响应
- **WHEN** 登录摘要或角色状态校验失败
- **THEN** 错误通过事件服务报告并跳过普通阶段，不创建成功页面、世界或提交部分更新的同步 Action

#### Scenario: 释放与结构检查
- **WHEN** 检查正式代码订阅位置并退出、重建主场景
- **THEN** 除 CacheBase 的公共实现机制外，订阅调用只位于缓存实现类和 Godot UI 类；缓存与 UI 释放各自句柄，服务不残留监听，新场景不会收到旧回调
