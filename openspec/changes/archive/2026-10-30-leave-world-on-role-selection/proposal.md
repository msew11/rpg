# Proposal

## Why

返回角色选择界面时，客户端清理了角色，但服务端在线成员仍保留，其他玩家要等该连接返回登录或断开才看见角色消失。返回选角应立即结束角色的在线世界状态并保留账号认证。

## What Changes

- 新增 LeaveWorld/LeaveWorldRt，通过当前连接身份退出角色世界。
- 返回选角清理前隔离旧代次，提交离开请求；服务器停止移动、撤销控制和观察者成员，其他玩家在下一次 AOI 发布移除角色。
- 验证重复离开、未认证拒绝、接管隔离和重新选角，并覆盖双进程真实 TCP 页面返回。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `server-v2-world-entry`：扩展返回边界，允许显式离开世界协议，保留既有保存职责。
- `server-v2-movement`：返回选角触发在线角色移除及控制释放。

## Impact

涉及 V2 外部协议、服务器运行时协调、SessionService、网络与 UI 回归及架构文档。无存档迁移，客户端和服务器需同时升级。
