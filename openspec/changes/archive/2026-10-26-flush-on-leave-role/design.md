## Context

MockServer.Flush 已负责角色与地图待写领域；MainController 的三个菜单出口最终清理角色会话，而 NetService.ResetSession 会丢弃该会话请求队列。

## Decision

由 MockTransport 实现可选的 IServerWriteBackTransport，NetService 在写回前推进零时间网络帧以发送已排队请求；MainController 在三个出口清理角色前调用。异常由出口显示并阻止离开，底层保留待写状态。正常传输 Dispose 的补写保持原样。

## Limits

只保证服务端已接纳并完成的状态。强制终止进程不会运行 Godot 退出回调或 Dispose；服务端尚未确认的移动不因零时间帧而完成。
