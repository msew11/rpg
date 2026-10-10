# Tasks

## 1. Windows 导出

- [x] 1.1 启用 Windows Desktop 预设的 PCK 嵌入，并在无伴随 PCK 的目录启动导出 exe 验证单文件运行。

## 2. Rider 入口

- [x] 2.1 新增共享 Rider 运行配置调用 `tools/check.ps1 -ExportWindows`，验证 XML 可解析且配置引用的命令成功执行。
- [x] 2.2 更新 README 的 Rider 导出说明，验证配置名称、命令和产物路径一致。

## 3. 集成检查

- [x] 3.1 运行 `powershell -ExecutionPolicy Bypass -File tools/check.ps1` 与 OpenSpec 校验，确认项目检查通过且变更记录完整。
