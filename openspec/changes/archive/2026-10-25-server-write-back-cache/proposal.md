## Why

Server 每次数据修改都写文件，移动与背包操作产生频繁 IO。改为内存优先、合并变更后按节点落地。

## What Changes

- 服务端请求只修改内存，按文件记录待写领域，定时仅提交变化内容。
- GM 提供检测间隔，保存至 user://.env，允许 0 或非负的 5 秒倍数，默认 5 秒；0 关闭定时检测。
- 正常关闭补写；失败保留内存和待写数据，错误通过网络错误入口报告并允许重试。
- **BREAKING** 请求成功表示内存提交成功，不再保证已刷盘；异常退出可能丢失尚未落地的数据。

## Capabilities

### New Capabilities
- `server-write-back`: 内存缓存、检测调度、失败重试与 GM 间隔设置。

### Modified Capabilities
- `mock-server`: 角色持久化改为延迟提交。
- `gm-item`: 发放成功以缓存提交为准。
- `role-bag`: 背包移动成功以缓存提交为准。
- `role-management`: 创建删除的重启恢复以完成落地为前提。

## Impact

涉及 Server 仓库、MockServer、EnvService、MockTransport、NetService、GM 面板及现有持久化验收，不改变协议或存档格式。
