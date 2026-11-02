# 验证记录

## 检查结果

- `powershell -ExecutionPolicy Bypass -File tools/check.ps1`：通过，包含 C# 编译、资源导入、默认启动场景。
- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests`：通过，普通 .NET 用例 150 个、Godot xUnit 用例 292 个及全部场景回归通过。等大同位修订的完整输出保留在忽略的 `build/monster-alignment-check.log`。
- 初次交付 `openspec validate add-gm-monster-placement --strict` 通过；整型主键及等大同位修订通过 `openspec validate gm-monster --type spec --strict`。
- Windows D3D12 图形模式与 headless 的 `tests/monster_placement_smoke.tscn`：通过；检查 `build/monster-placement.png`，确认树妖正常比例贴图、等大透明虚影及坐标回显。

## 新增回归

- `MonsterConfigTests`：正式树妖整型主键及 int 索引、缓存释放、字符串/小数/非正数/溢出 ID 拒绝、重复 ID、缺字段、缺图、空名称、越界及负锚点。
- `ServerMonsterTests`：正式字节协议认证、非法命令（含非正整型及溢出 ID）零地图变更、唯一创建且不发背包通知、两个静止观察者收到同一对象、返回选角及服务器重启保持 ID/类型/坐标。
- `MonsterPlacementSmoke`：真实候选点击只选择、回车/小键盘回车预览、确认前无 GM 反馈或地图创建、等大半透明虚影、UI/越界点击隔离、缩放逆投影、单次创建不移动、右键取消、取消恢复移动、返回选角/控制失效/离树清理与场景重建。
- 对齐回归在 0.5、1、2、1.75 倍缩放和相机平移后验证静止鼠标仍与虚影锚点对齐；每次真实点击后，服务器逻辑坐标精确等于点击前虚影坐标，正式怪物可见矩形四角与虚影逐角误差小于 0.02 视口像素，防止尺寸或位置跳变。

图形补验暴露测试在 ECS 更新后早一帧读取贴图节点的竞态，已改为等待正式表现节点出现，并在图形及 headless 模式复验通过；产品创建与同步流程未因此修改。

## 当前边界

仅树妖静态首帧展示；没有 AI、战斗、怪物属性或刷怪规则。正常保存与退出后恢复地图对象，强制结束进程仍沿用现有延迟保存限制。
