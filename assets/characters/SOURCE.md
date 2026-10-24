# 基础角色与默认头部资源

来源为用户提供的 `art_export/基础角色_男女全部动作/animation_frames.json` 及该目录引用的透明 PNG。原始身体 ID 为男性 A141、女性 A142；共 26 个动作、605 帧。默认头部来自同一导出目录的 `raw/avatar/head/head0101.gsn`（男）与 `head0151.gsn`（女），各含五方向。原始 `raw/res/uires/actcreate.lua` 的创建角色配置使用 `head=1`；这里采用第一套男女头型。项目中保存组合后的无损图集和 Godot 动画资源，不需要 GSN 解码器或外部目录。

每张图集一方向一行，每行按原始帧序排列。`dir0` 至 `dir4` 依次为左下、左、左上、上、下；右上、右、右下由相应左侧方向镜像。映射和定位点沿用来源动画查看器，未推断原始游戏的其他动作规则。

每个 `.tres` 保存 `SpriteFrames` 和该动作的原始 `Anchor`，运行时将锚点对齐玩家逻辑位置的等轴投影。暂用预览速度每秒 10 帧；站立、跑步循环，其他动作一次播放并保持末帧。当前图集已合入固定的默认头部及配套头发，不包含装备和坐骑，也不提供运行时换装。

在项目根目录使用 Windows PowerShell 重建（将路径替换为实际素材目录）：

```powershell
powershell -ExecutionPolicy Bypass -File tools/import_actor_animations.ps1 -SourcePath '<素材目录>/基础角色_男女全部动作' -RawPath '<素材目录>/raw'
```

头部预处理使用 Python 3 与 Pillow（`python -m pip install Pillow`），图集使用 Windows 自带的 System.Drawing。工具核对性别、动作、方向、帧数和画布尺寸后生成项目内资源。身体 DIMG 像素块后首组挂接点与头部帧连接点均相对各自裁剪块；先加回裁剪偏移，再对齐连接点。按每个动作所有帧的合并边界扩大画布，并将 Anchor 平移相同距离，保持脚下定位不变。头身合入同一帧，播放和镜像自然同步。

中间帧写入被忽略的 `build/actor-composite-input`；来源文件只读。`head_composition.json` 保存原始身体/头部文件 SHA256、身体画布与 Anchor、合成平移量，方便核对来源。运行和导出只依赖项目内图集与 `.tres`。后续运行 `tools/check.ps1` 让 Godot 导入图集。

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
