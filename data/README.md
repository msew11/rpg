# 手工测试区块

`manual_chunks.json` 是连续世界原型的手工配置数据，不代表最终地图制作方式。当前世界横纵各有 4 个区块；每区块为 12 × 12 个地块。`layout[y][x]` 给每个规范区块 ID 指定一个手工图案，图案中的 `.` 使用 Kenney Isometric Roads 的 `grass.png`，`#` 使用同包的 `dirt.png`。素材许可与来源见 `assets/kenney_isometric_roads/SOURCE.md`。

行走坐标不回绕，数据 ID 在 4 × 4 个区块中回环。编辑 `patterns` 或 `layout` 可调整测试区域布局；同一次运行中玩家的地块切换写入内存状态，不修改此配置文件。
