## Why

运行时脚本 `test_chunk_catalog.gd` 的 `test_` 前缀容易被当作可执行测试。项目还保留了已删除脚本 `test_ground.gd` 的孤立 UID。

## What Changes

- 将手工区块读取器改名为 `manual_chunk_catalog.gd` / `ManualChunkCatalog`，配置文件改为 `manual_chunks.json`，并更新引用与文档。
- 将对应的测试脚本改名为 `test_manual_chunk_catalog.gd`，保持测试前缀仅用于可执行测试。
- 删除没有同名脚本的 `scripts/view/test_ground.gd.uid`；随改名脚本移动其有效 UID。

## Capabilities

### Modified Capabilities

- `world-script-boundaries`: 更新脚本目录中手工区块读取器的名称。
- `development-conventions`: 明确非测试脚本不使用 `test_` 文件名前缀。

## Impact

仅调整命名和过期元数据，不改变区块内容、运行行为或核心脚本。
