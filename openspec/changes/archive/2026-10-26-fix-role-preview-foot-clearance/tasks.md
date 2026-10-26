# Tasks

## 1. 预览留白修复

- [x] 1.1 调整选角和创角的预览比例与底部定位间距，更新架构说明和路线图，并通过角色界面场景验证男女八方向及三种窗口尺寸的完整头身和按钮间距。

## 2. 集成检查

- [x] 2.1 运行 tools/check.ps1、角色管理与装备场景，核验页面及装备预览回归；图形模式保存并检查修复后的页面截图。

检查结果：tools/check.ps1 通过（编译零警告、零错误，项目导入和主场景通过）；角色管理 headless 与 OpenGL 图形模式均通过，装备场景通过。已目视检查 build/role-selection-preview.png 和 build/role-create-small.png，脚部完整且旋转按钮留有间距。

## Workflow follow-up

- 完成后同步规范、归档 change，并以中文约定式提交保留本地改动。
