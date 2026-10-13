/// <summary>用于验证 ECS 组件替换的坐标测试组件。</summary>
public partial class TestPositionComponent : EcsComponent
{
    /// <summary>区分新旧组件实例的坐标。</summary>
    public double X { get; set; }

    /// <summary>初始化测试坐标。</summary>
    public TestPositionComponent(double x = 0) => X = x;

    /// <summary>把测试坐标转换为不依赖区域设置的字符串。</summary>
    public override string ToPersistedString() => X.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>从测试字符串恢复坐标。</summary>
    public override void FromPersistedString(string value) =>
        X = double.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
}
