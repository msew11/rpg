# Tasks

## 1. 地图数据与分块仓库

- [x] 1.1 实现不可变 World 数据、Context 内容比较与 RepoWorld 清单提交；以独立临时目录测试缺失、结构错误、变化块、空地图、中断、清理及安全路径，并记录存储格式到 server-v2.md

## 2. Actor 与宿主接入

- [x] 2.1 实现 INIT 加载、邮箱保存与必需仓库装配，保存 Role→World→Login；测试首消息屏障、地图隔离、未知地图、修复重试、保存串行和失败隔离，更新架构与路线图
- [x] 2.2 用 World 修改验证周期禁用、动态间隔、显式屏障和关闭补写，保留 Login/Role 与旧客户端验收

## 3. 集成检查

- [x] 3.1 运行 tools/check.ps1 -Tests、严格 OpenSpec 校验并核对代码/规格/文档；记录检查结果与限制

## Workflow follow-up

- 同步增量规格，归档 change，创建独立中文 Conventional Commit，不推送。
