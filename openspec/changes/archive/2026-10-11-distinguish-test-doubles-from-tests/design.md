## 决定

- 只有由 `tools/check.ps1` 直接执行的测试脚本使用 `test_` 前缀。
- `tests/doubles/` 下的测试替身使用 `double_` 前缀；对应顶层 GDScript 类使用 `Double` 前缀，让 Rider 中的类型名也表达其用途。
- 重命名 `test_memory_data_source.gd` 为 `double_memory_data_source.gd`，移动同 UID 的 `.gd.uid`，并同步预加载引用及文档。
- 本次不修改历史归档 change；新 change 记录对旧命名约定的修订。

## 验证

搜索旧路径和旧类名，核对 UID；运行 `tools/check.ps1` 验证测试仍可加载。
