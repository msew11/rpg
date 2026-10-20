using Godot;
using Rpg.World.Actions;

namespace Rpg.Input;

/// <summary>独立处理输入事件并将世界点击转为 Action，供后续 UI 输入路由扩展。</summary>
public sealed class InputSystem
{
    /// <summary>下一条本地移动意图的单调序号。</summary>
    private long _nextActionId;

    /// <summary>把未被 UI 消费的左键点击转换为具有单调序号的移动 Action。</summary>
    public MoveAction? TranslateWorldClick(InputEvent input, Vector2 position) =>
        input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }
            ? new MoveAction(++_nextActionId, position)
            : null;
}
