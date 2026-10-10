## Why

分支归属约定现在要求 `main` 保留三份零字节占位文档，但现有 GitHub Actions 仍要求它们不存在。这使正确的占位文件提交在指向 `main` 的 PR 中失败，也无法防止后续提交删除占位文件。

## What Changes

- 调整 `main` 的文档归属检查：三份文档都必须存在，且是零字节的普通文件。
- 继续在指向 `main` 的 PR 和 `main` 的 push 上运行检查；`develop` 的共享文件检查保持原样。

## Capabilities

只更新仓库 CI 约定，不改变游戏能力，跳过规格增量。

## Impact

影响 `.github/workflows/branch-document-ownership.yml`。`main` 的空白占位文件通过；缺失、非普通文件或包含内容的文件失败。
