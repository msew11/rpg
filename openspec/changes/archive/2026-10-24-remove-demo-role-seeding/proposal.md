## Why

已有完整创建角色流程，新存档仍自动生成两个演示角色，绕过了玩家自行创建角色的入口。现在应让首次登录从空角色列表开始，由玩家主动选择外观并创建自己的第一个角色。

## What Changes

- 新存档登录返回空列表，读取缺失角色仅报错，不生成演示角色。
- 保留已有角色、墓碑及旧 initialized 文件兼容，不迁移或删除现有角色。
- 创建角色直接使用原有新角色初值，保留男女出生坐标、编号规则及持久化行为。
- 测试显式准备已有角色；增加首次空列表、首次创建和重启回归。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `client-session`: 首次登录为空列表，性别验证使用已创建角色。
- `role-management`: 全新存档可创建首个角色，缺失角色不自动生成。
- `client-actor-presentation`: 男女外观验收基于已创建角色。

## Impact

RepoServerRole、测试数据夹具、角色管理界面验收、架构与路线图；协议与账号认证方式不变。
