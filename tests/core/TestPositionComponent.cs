/// <summary>用于验证 ECS 组件替换的坐标测试组件。</summary>
public partial class TestPositionComponent : EcsComponent
{
    /// <summary>区分新旧组件实例的坐标。</summary>
    public double X { get; set; }

    /// <summary>初始化测试坐标。</summary>
    public TestPositionComponent(double x = 0) => X = x;
}
