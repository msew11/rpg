# Tasks

## 1. 名称与路径

- [x] 1.1 将 Godot 展示脚本及 UID 移到 `scripts/ui/`，同步场景资源路径；以无界面导入和主场景启动验证资源可加载。
- [x] 1.2 将世界只读入口改为 `chunks_view()`、`debug_view()` 并更新全部生产与测试调用；以现有只读、移动、区块和存档测试验证行为。
- [x] 1.3 更新架构、路线图和规格描述，并搜索当前文件中旧目录与旧方法调用，确认命名一致。

## 2. 集成检查

- [x] 2.1 运行 `powershell -ExecutionPolicy Bypass -File tools/check.ps1` 并处理失败项。

## Workflow follow-up

- 检查完成后同步规格并归档 change。
