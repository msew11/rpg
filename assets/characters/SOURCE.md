# 基础角色身体资源

来源为用户提供的 `art_export/基础角色_男女全部动作/animation_frames.json` 及该目录引用的透明 PNG。原始身体 ID 为男性 A141、女性 A142；共 26 个动作、605 帧。项目中仅保存解码 PNG 的无损图集和 Godot 动画资源，不需要 GSN 解码器或外部目录。

每张图集一方向一行，每行按原始帧序排列。`dir0` 至 `dir4` 依次为左下、左、左上、上、下；右上、右、右下由相应左侧方向镜像。映射和定位点沿用来源动画查看器，未推断原始游戏的其他动作规则。

每个 `.tres` 保存 `SpriteFrames` 和该动作的原始 `Anchor`，运行时将锚点对齐玩家逻辑位置的等轴投影。暂用预览速度每秒 10 帧；站立、跑步循环，其他动作一次播放并保持末帧。身体不包含独立头部、发型、装备和坐骑。

在项目根目录使用 Windows PowerShell 重建（将路径替换为实际素材目录）：

```powershell
powershell -ExecutionPolicy Bypass -File tools/import_actor_animations.ps1 -SourcePath '<素材目录>/基础角色_男女全部动作'
```

工具只使用 Windows 自带的 System.Drawing，核对性别、动作、方向、帧数和画布尺寸后生成项目内资源。后续运行 `tools/check.ps1` 让 Godot 导入图集。

资源链路回归检查：

```powershell
dotnet test tests/ClientTests/ClientTests.csproj
godot-mono --headless --path . tests/actor_animation_smoke.tscn
godot-mono --headless --path . tests/client_smoke.tscn
```

需人工核对时，可执行以下命令，在被忽略的 `build/actor-preview.png` 生成男女八方向的 Godot 渲染预览：

```powershell
godot-mono --path . --rendering-method gl_compatibility tests/actor_animation_smoke.tscn -- --capture-actors
```
