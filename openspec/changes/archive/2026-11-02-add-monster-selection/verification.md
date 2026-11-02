# 检查记录

- `powershell -ExecutionPolicy Bypass -File tools/check.ps1` 通过：编译零警告、零错误，项目导入和主场景启动通过。
- 最终 `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests` 完整通过：普通 .NET 150/150、Godot xUnit 292/292，全部正式场景回归通过，包括新增怪物选中场景。
- `monster_selection_smoke.tscn` 在 headless 固定 60 FPS 与 Windows OpenGL 图形模式均通过：真实左键选择/切换与右键取消、重复选择保持进度、六帧循环、红剑原像素与热点、透明及重叠命中、0.5/1/2/1.75 倍缩放和平移、UI/模态/失焦/GM 放置隔离、不移动与空地移动、AOI 移出、离开与重入清理。
- 图形截图 `build/monster-selection.png` 已目视核对：圆环以原始锚点贴合树妖脚底，等比扩至目标宽度后外圈清晰，绘制在树根后方。硬件鼠标不进入视口截图；专项通过当前硬件鼠标纹理像素与热点核验 AttackH。
- 资源比对：导入圆环首帧与原导出预览逐像素一致，红剑与原 AttackH 预览逐像素一致。保留原始六帧顺序，其中对称回缩帧复用相同像素；哈希和公共锚点记录在 source.json。
- 首轮完整检查在既有 ground item drop 场景遇到 Godot `gchandle.is_released()` 致命错误；最终完整重跑通过该场景及全部后续检查，未修改物品丢弃实现或放宽断言。
- `openspec validate add-monster-selection --strict`、`git diff --check` 通过。选择仅属于客户端表现，未加入攻击、协议或持久化状态。
