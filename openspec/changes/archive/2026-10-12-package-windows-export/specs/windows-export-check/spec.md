# Spec Delta

## ADDED Requirements

### Requirement: Windows 导出检查提供完整压缩包

检查脚本 SHALL 在 Windows .NET 导出与启动验证成功后生成 ZIP，包含可执行文件和导出时生成的运行时依赖，并保持相对目录结构。ZIP 位于导出目录之外，重复运行时更新同一路径的包。

#### Scenario: 成功导出并打包

- **WHEN** 开发者运行 `tools/check.ps1 -ExportWindows` 且检查、导出和启动均成功
- **THEN** `build/rpg-windows.zip` 存在，解压后顶层有 `rpg.exe` 与所需的运行时目录，完整解压的程序能够启动

#### Scenario: 导出或启动失败

- **WHEN** Windows 导出或导出程序启动验证失败
- **THEN** 本次运行不发布新的压缩包，脚本报告失败

#### Scenario: 重复导出

- **WHEN** 开发者再次运行 `tools/check.ps1 -ExportWindows`
- **THEN** 新压缩包替换旧压缩包，先前的 `build/` 产物不会成为新游戏资源
