# Tasks

## 1. ECS 动态对象

- [x] 1.1 扩展 EntityStore 和 WorldManager，通过 Action 同步 AOI 对象并暴露只读 ECS 状态
- [x] 1.2 让拾取与世界表现读取 ECS，并补充生命周期回归测试

## 2. 表现分层与素材

- [x] 2.1 将地面名称、信息框、布局和点击移至 UI，WorldView 只保留世界图像和命中；更新场景验收
- [x] 2.2 把素材目录及图集静态方法移至 Utils，轮廓归 WorldView，删除 Items 并回归图鉴和背包

## 3. 集成

- [x] 3.1 更新架构和路线图，验证依赖方向并运行 tools/check.ps1

## Workflow follow-up

- 检查通过后归档 change，再创建本地中文约定式提交。
