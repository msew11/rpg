# 设计

Service : IService 仅包含 public bool IsDisposed { get; protected set; }、protected ThrowIfDisposed() 和 abstract Dispose()。接口给出只读契约，基类共享实际状态。派生类不能再声明 _disposed 或重复的状态属性。

不引入 OnDispose 钩子或统一释放模板：网络、会话需要先标记再清理，设置服务必须在标记前保存，且保存失败也在 finally 标记释放。各服务现有 Dispose 保留顺序并改写继承状态。

SettingService、WorldService 原来只做释放检查的 EnsureActive 删除，直接调用基类方法；SessionService.EnsureActive 还校验网络和代次，保留并复用基类检查。Config、Animation、Events、Net 的重复 ThrowIf 统一调用基类方法。Registration、CacheBase、MockTransport 不属于 IService，保持自身生命周期。

验收全部服务的接口状态从未释放到已释放，重复释放幂等；网络关闭或设置保存异常后仍正确标记状态并清理其余服务。复用既有协议、事件重入、设置保存及重启测试，补充面向契约的状态验证。
