# 角色逐帧审查导出

在项目根目录执行：

```powershell
powershell -ExecutionPolicy Bypass -File tools/export_actor_review.ps1 -Python <安装了Pillow的Python路径>
```

需要 Godot 4.7.2 .NET（`GODOT_BIN` 或 `godot-mono`）、.NET SDK、Python/Pillow 和微软雅黑或 Noto Sans CJK 字体。可用 `-OutputDirectory <新目录绝对路径>` 指定输出；默认放在 `build/actor-review-日期时间/`。已有目录与ZIP拒绝覆盖。导出启动GPU窗口时使用隐藏模式，不读取正式账号或存档。

工具直接渲染正式 `ActorView`，保持当前身体、头部、武器、镜像、挂点和遮挡。按配置和槽位规则枚举所有允许主副手组合，空槽也参与枚举作为对照；当前为9组合、男女13动作、八方向，共234组、8,728张独立PNG。仅普通剑可同时装备副手盾牌，其他主手武器要求副手为空；盾牌可单独装备。固定使用默认头型1，不枚举所有头型。

输出内容：

- `index.html`：双击离线浏览。筛选性别、装备、动作；上一帧、下一帧、播放，整数倍像素放大和背景切换。帧号从0开始。
- `frames/<性别>/main-<主手ID>_off-<副手ID>/<动作>/d<方向>-<方向名>-f<帧号>.png`：透明独立帧。
- `labelled/`：带中文方向和帧号的联系表，行为八方向，列为身体帧。
- `sheets/`：透明原始实绘联系表，全部图保持相同画布和角色定位点。
- `manifest.json`：来源版本、头型、画布、方向、帧时长、实际左右手武器资源和采样帧；`manifest.js` 为离线页面整理后的索引。
- `coverage.csv`：各组身体帧数、方向数、文件路径及覆盖清单。
- 同级 `.zip` 和 `.zip.sha256`：完整压缩包及校验值。

方向顺序为 `0=↓、1=↙、2=←、3=↖、4=↑、5=↗、6=→、7=↘`，与正式方向映射核对。每个身体帧都导出；武器缺少独立动作时，保留游戏当前的素材复用，并记录实际资源和武器帧号。非循环动作在浏览页可以重复播放，便于审查首尾。

镜像沿用原客户端：主手固定源右手，副手固定源左手，整个组合镜像。拳刃、飞刀只保存一个主手装备实例：拳刃右手来自 `1017/`、左手来自 `1017/left/`；飞刀右手来自 `1018/right/`、左手来自 `1018/`。实际副手槽为空，左右图像均由主手派生。不换手、不补柄、不拆枪杖。七件装备及双手变体共234个动作入口，3150帧引用。

`motion`为请求动作，`actualMotion`为按装备解析的身体动作，页面显示“请求→实际”。剑/大剑使用攻击1/准备1，枪杖使用攻击2/准备2，拳刃/飞刀使用攻击3/准备3。`sourceAction`记录每只武器实际来源编号，缺少动作回退站立1；采样以身体帧取余，层次始终由当前身体动作/帧决定。清单版本3中`main/off`仍记录实际槽位编号，`samples.off`记录视觉左手资源，成对武器时其itemId为主手编号。`nativeRightHand`与`sourceRightHand`记录固定源手，镜像不改变它们。

导出前遍历全部组合的可见像素范围，统一画布并保留边距，防止长柄或攻击姿态裁剪。整理阶段验证每组、每方向和每帧完整、非空、不接近边界，独立PNG像素等于GPU联系表中的对应区域，ZIP逐文件CRC通过。输出目录的 `.gdignore` 阻止编辑器把审查图片再次导入游戏资源，图片和压缩包不提交Git。

渲染、整理可以分步执行，以便保留诊断：

```powershell
godot-mono --path . res://tests/actor_review_export.tscn -- --output=<新目录绝对路径> --revision=<Git版本>
python -B tools/build_actor_review.py <该输出目录>
```

审查时请记录“性别、主手ID、副手ID、动作、方向号、身体帧号”，可以从独立PNG路径或页面直接定位。导出保留当前表现，发现问题后另行记录与修复。

独立来源合成验收（需要Python/Pillow/NumPy及只读原素材和指定客户端）：

```powershell
python -B tools/verify_actor_composition.py --raw <art_export/raw> --write-fixture tests/fixtures/actor_weapon_sources.json
python -B tools/verify_actor_composition.py --raw <art_export/raw> --body-manifest <基础角色_男女全部动作/animation_frames.json> --client <QQ幻想/foplayer.exe> --review <GPU输出目录>
```

第一条核验生成的原始PNG和挂点并重建C#黄金摘要，第二条生成独立来源参考并核对GPU，写入 `source-reference-report.json`；不通过时输出 `reference-diffs/`，不以正式表现结果更新期望。比较预乘颜色（每通道容差2）与alpha（容差1）以覆盖GPU8位混合舍入，不允许位置、帧或层次差异。参考的身体锚点与帧偏移来自只读原始导出清单，武器、头部像素和全部挂点直接从原始DIMG读取；不是运行原客户端取得的截图。

角色贴图关闭Godot的低alpha自动边缘改色和AtlasTexture的filter_clip，保留完整源像素。透明视口预乘颜色转为直通alpha保存PNG，避免浏览器再次乘alpha产生暗边；参考比较使用预乘颜色处理GPU8位舍入。
