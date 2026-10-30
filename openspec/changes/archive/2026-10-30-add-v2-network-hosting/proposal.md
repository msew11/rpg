# Proposal

## Why
V2 当前依赖启动时创建的进程内服务器，无法连接另一进程，也无法仅作为客户端运行。需要将服务器生命周期与客户端登录分离，为多人移动建立真实传输基础。

## What Changes
- 提取普通 MockServer，MockServerService 启动时为空，登录时按模式持有服务器。
- 增加异步 TCP、长度分帧、有界输出、超时和远程代次适配。
- 登录提供服务器与纯客户端选项，默认入口切换 V2，旧测试显式使用旧宿主。
- 本地保存等待此前 TCP 请求响应，再执行服务器写回；远程无保存能力。

## Capabilities
### New Capabilities
- `server-v2-hosting`: 登录驱动的服务器生命周期和 TCP 托管。
### Modified Capabilities
- `server-v2-network`: 接纳远程更高代次并继续隔离旧结果。
- `server-v2-world-entry`: 默认启动 V2，保留初始只读能力。

## Impact
影响 V2 Hosting、ConnectionSession、Net、Session、MainController 和测试装配；保持四字段信封与业务协议不变。
