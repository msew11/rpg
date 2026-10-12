using Godot;

/// <summary>标记区块拥有的怪物实体，并在区块落地时保留其身份。</summary>
[GlobalClass]
public partial class MonsterComponent : EcsComponent
{
    /// <summary>用空字符串保存无额外数据的怪物身份。</summary>
    public override string ToPersistedString() => "";

    /// <summary>恢复怪物身份标记。</summary>
    public override void FromPersistedString(string value) { }
}
