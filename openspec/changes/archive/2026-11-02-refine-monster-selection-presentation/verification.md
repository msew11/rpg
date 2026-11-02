# 检查记录

- 对原始 magic9002.gso 重新顺序解码全部六帧，与项目 PNG 逐像素一致；原始画布为 69×43。红剑迁移后与原 AttackH 预览逐像素一致，圆环 PNG 与其他鼠标 PNG 均未改变。
- 当前树妖宽 139，圆环放大约 2.014 倍。先前 Nearest 明显放大台阶；本次只对圆环使用 Linear，保留画布、锚点、显示尺寸和六帧播放。
- `powershell -ExecutionPolicy Bypass -File tools/check.ps1` 通过，编译零警告、零错误，项目导入和主场景启动通过。
- 怪物选中、鼠标反馈 headless 固定 60 FPS 专项通过。Windows OpenGL 图形模式下怪物选中专项通过，截图 `build/monster-selection.png` 已目视确认脚底环边缘更柔和；`build/selection-filter-comparison.png` 提供原尺寸、Nearest 与 Linear 的同帧对比。
- 首轮图形验收暴露快速移动后测试复用旧相机坐标的问题；测试在点击前重新读取实际可见像素，保留原选择断言，最终 headless 与图形验收通过。
- `openspec validate refine-monster-selection-presentation --strict` 和 `git diff --check` 通过。现有交互规格未改变，本次使用 skip_specs 记录内部路径与显示参数修正。
- 平滑过滤不会增加原图细节，不据此推断原客户端的过滤或混合模式。
