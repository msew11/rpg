# Tasks

## 1. 角色缓存扁平化

- [x] 1.1 将三份缓存与候选校验迁入 ServerRoleCache，迁移仓库、Handler、Actor 和现有测试并删除旧类；更新架构与路线图，补充副本隔离、领域提交及跨表拒绝测试，以构建和相关测试验证。

## 2. 集成验证

- [x] 2.1 运行 tools/check.ps1 -Tests，核对摘要加载、装备与地面物品共同恢复等回归结果，记录验收结果并通过 OpenSpec 校验。

## Workflow follow-up

- 全部检查完成后归档 change，创建中文约定式本地提交，不推送。
