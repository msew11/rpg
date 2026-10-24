# 设计

私有依赖用只读字段保存；Registration 对外暴露其服务集合，保留只读属性。带计算或校验的属性保留，不一律改为字段。

SettingService 的系统与角色写入已有同一 Write 实现，业务方法直接调用它。SetHudPosition、ResetHudPositions 第一条语句访问 CurrentRole，已检查释放和选角状态，因此删除前置重复 EnsureActive。其他校验顺序保持。

HUD 的 List<IDisposable> 与 MainController、CacheBase 的监听所有权相同，统一成员名称即可；不引入订阅容器或基类。

验收复用设置持久化、角色隔离、地图边界和场景订阅释放测试，执行 tools/check.ps1 及客户端冒烟。无需新增只验证成员名称的测试。
