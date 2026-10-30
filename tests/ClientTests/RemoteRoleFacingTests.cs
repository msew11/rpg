using Godot;
using Rpg.Const;
using Rpg.Session.Data;
using Rpg.World;

namespace ClientTests;

/// <summary>验证 openspec/specs/server-v2-movement 的远程停止朝向、独立状态与可见生命周期。</summary>
[Trait("Runtime", "Godot")]
public sealed class RemoteRoleFacingTests
{
    /// <summary>八方向移动后停止，静止样本淘汰全部移动历史也不改变朝向。</summary>
    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(-1, 0)]
    [InlineData(-1, -1)]
    [InlineData(0, -1)]
    [InlineData(1, -1)]
    public void StopRetainsFacingAfterMovingSamplesExpire(int x, int y)
    {
        var direction = new Vector2(x, y).Normalized();
        var start = new Vector2(100, 100);
        var end = start + new Vector2(x, y) * 10;
        var buffer = new RemoteRoleBuffer();
        Accept(buffer, 1000, Role(5, start, end));
        var moving = Assert.Single(buffer.Render(1000));
        Assert.Equal(RoleMotion.Run, moving.Motion);
        Assert.True(moving.Facing.IsEqualApprox(direction));
        for (var time = 1100; time <= 2000; time += 100)
        {
            Accept(buffer, time, Role(5, end));
            var stopped = Assert.Single(buffer.Render(time));
            Assert.Equal(RoleMotion.Stand, stopped.Motion);
            Assert.True(stopped.Facing.IsEqualApprox(direction));
        }
    }

    /// <summary>改向和停止都在下一次呈现前收到时，仍采用最后目标方向。</summary>
    [Fact]
    public void TurnAndStopBetweenRendersRetainNewDirection()
    {
        var buffer = new RemoteRoleBuffer();
        Accept(buffer, 1000, Role(5, Vector2.Zero, Vector2.Right * 100));
        Accept(buffer, 1100, Role(5, Vector2.Right * 10, new(10, -100)));
        Accept(buffer, 1200, Role(5, Vector2.Right * 10));
        Assert.Equal(Vector2.Up, Assert.Single(buffer.Render(1200)).Facing);
        // 较旧或同时间样本不能污染已经接纳的朝向。
        Accept(buffer, 1100, Role(5, Vector2.Zero, Vector2.Left));
        Accept(buffer, 1200, Role(5, Vector2.Zero, Vector2.Left));
        Assert.Equal(Vector2.Up, Assert.Single(buffer.Render(1300)).Facing);
    }

    /// <summary>外推恰好到达目标以及目标等于当前位置时，站立保留有效方向。</summary>
    [Fact]
    public void EndpointAndFinalDisplacementPreserveFacing()
    {
        var buffer = new RemoteRoleBuffer();
        Accept(buffer, 1000, Role(5, Vector2.Zero, Vector2.Left * 10));
        var endpoint = Assert.Single(buffer.Render(1100));
        Assert.Equal(Vector2.Left * 10, endpoint.Position);
        Assert.Equal(RoleMotion.Stand, endpoint.Motion);
        Assert.Equal(Vector2.Left, endpoint.Facing);
        Accept(buffer, 1100, Role(5, Vector2.Left * 10));
        // 最后位置增量给出末段方向，即使服务器保留了等于当前位置的目标。
        Accept(buffer, 1200, Role(5, new(-10, -10), new(-10, -10)));
        Assert.Equal(Vector2.Up, Assert.Single(buffer.Render(1200)).Facing);
    }

    /// <summary>不同角色的朝向隔离，离开视野及清空世界后不继承旧朝向。</summary>
    [Fact]
    public void FacingIsIndependentAndClearedWithVisibility()
    {
        var buffer = new RemoteRoleBuffer();
        Accept(buffer, 1000, Role(5, Vector2.Zero, Vector2.Left), Role(6, Vector2.Zero, Vector2.Up));
        Accept(buffer, 1100, Role(5, Vector2.Zero), Role(6, Vector2.Zero));
        var stopped = buffer.Render(1100).ToDictionary(actor => actor.RoleId);
        Assert.Equal(Vector2.Left, stopped[5].Facing);
        Assert.Equal(Vector2.Up, stopped[6].Facing);
        Accept(buffer, 1200, Role(6, Vector2.Zero));
        Assert.Equal(6, Assert.Single(buffer.Render(1200)).RoleId);
        Accept(buffer, 1300, Role(5, Vector2.Zero), Role(6, Vector2.Zero));
        var reentered = buffer.Render(1300).ToDictionary(actor => actor.RoleId);
        Assert.Equal(Vector2.Down, reentered[5].Facing);
        Assert.Equal(Vector2.Up, reentered[6].Facing);
        buffer.Clear();
        Assert.Empty(buffer.Render(1400));
        Accept(buffer, 1500, Role(6, Vector2.Zero));
        Assert.Equal(Vector2.Down, Assert.Single(buffer.Render(1500)).Facing);
    }

    /// <summary>构造只包含公开外观和权威移动的远程角色样本。</summary>
    private static DataPublicRole Role(long id, Vector2 position, Vector2? target = null) => new(id,
        new(id, "远程角色", 1, RoleGender.Male, 1, 0), new(id, 100, 100, 60, 60, 0),
        new(id, 1, position, 0, 0) { Target = target, Speed = 100 }, RoleWorldEquipmentPbData.Empty(id));

    /// <summary>以完整可见集合接纳一帧，复用正式缓冲入口。</summary>
    private static void Accept(RemoteRoleBuffer buffer, long time, params DataPublicRole[] roles) =>
        buffer.Accept(time, roles.ToDictionary(role => role.RoleId));
}
