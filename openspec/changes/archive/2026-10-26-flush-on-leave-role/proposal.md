## Why

客户端与 MockServer 同进程，定时写回未到期时退出角色可能使待写数据在进程被结束后丢失。

## What Changes

- 系统菜单离开角色前同步提交服务端已接纳的待写文件。
- 写盘失败保留当前角色界面与待写状态，允许重试。
- 强制杀进程仍可能丢失尚未落地的数据。

## Capabilities

### Modified Capabilities

- `server-write-back`: 增加离角提交与失败重试场景。

## Impact

修改本地传输、网络服务、主场景退出流程和相应测试，不改变 protobuf 协议或存档格式。
