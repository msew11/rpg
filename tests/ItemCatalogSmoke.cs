using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using Rpg.Services;
using Rpg.Tests.Doubles;
using Rpg.UI;
using FileAccess = Godot.FileAccess;

namespace Rpg.Tests;

/// <summary>验收 openspec/specs/item-catalog 的完整目录、分类检索、分页、详情与真实 GM 入口。</summary>
public partial class ItemCatalogSmoke : Node
{
    /// <summary>隔离存档并自动操作正式 HUD，以退出码报告回归结果。</summary>
    public override async void _Ready()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rpg-catalog-" + Guid.NewGuid());
        DoubleMainController? main = null;
        try
        {
            var entries = JsonSerializer.Deserialize<DataItemAsset[]>(FileAccess.GetFileAsString("res://assets/items/catalog.json"))!;
            Check(entries.Length == 22872 && entries.Select(entry => entry.Id).Distinct().Count() == entries.Length,
                "素材缺失或编号重复");
            var counts = new[] { 6612, 6097, 1451, 8712 };
            var groups = entries.GroupBy(entry => entry.Category).ToArray();
            Check(groups.Length == 4 && groups.Select(group => group.Count()).SequenceEqual(counts), "四类素材数量不符");
            foreach (var atlas in entries.GroupBy(entry => entry.Atlas))
            {
                var texture = GD.Load<Texture2D>(atlas.Key);
                Check(texture is not null, "缺失项目内图集");
                foreach (var entry in atlas)
                    Check(entry.X >= 0 && entry.Y >= 0 && entry.W > 0 && entry.H > 0
                        && entry.X + entry.W <= texture!.GetWidth() && entry.Y + entry.H <= texture.GetHeight()
                        && entry.CropX + entry.W <= entry.Width && entry.CropY + entry.H <= entry.Height
                        && entry.Sha256.Length == 64, "素材区域或溯源数据无效：" + entry.Id);
            }
            main = new DoubleMainController { SettingsDirectory = directory, ServerDirectory = Path.Combine(directory, "server") };
            AddChild(main);
            await Frame();
            main.FindChildren("*", "Button", true, false).OfType<Button>().Single(button => button.Text == "登录")
                .EmitSignal(Button.SignalName.Pressed);
            await Frames();
            Press(main, "SelectRole1001");
            Press(main, "EnterRole");
            await Frames();
            var hud = main.FindChildren("*", "Control", true, false).OfType<GameHud>().Single();
            hud.FindChildren("*", "Button", true, false).OfType<Button>().Single(button => button.Text == "GM")
                .EmitSignal(Button.SignalName.Pressed);
            Press(hud, "OpenItemCatalog");
            await Frames();
            var catalog = (ItemCatalogControl)hud.FindChild("ItemCatalog", true, false);
            Check(catalog.Visible && !hud.GetNode<Control>("GM").Visible, "GM 和图鉴未互斥");
            var grid = (GridContainer)catalog.FindChild("CatalogGrid", true, false);
            var categories = (OptionButton)catalog.FindChild("CatalogCategory", true, false);
            var search = (LineEdit)catalog.FindChild("CatalogSearch", true, false);
            Check(categories.ItemCount == 5 && grid.GetChildCount() == 48, "分类或初始分页错误");
            for (var category = 1; category <= 4; category++)
            {
                categories.Select(category);
                categories.EmitSignal(OptionButton.SignalName.ItemSelected, category);
                var sources = grid.GetChildren().OfType<Button>().Select(button => button.TooltipText).ToArray();
                Check(sources.All(source => groups[category - 1].Any(entry => entry.Source == source)), "混入其他分类");
                Press(catalog, "CatalogNext");
                Check(!sources.Contains(grid.GetChild<Button>(0).TooltipText), "下一页没有更新");
                Press(catalog, "CatalogPrevious");
                Check(sources[0] == grid.GetChild<Button>(0).TooltipText, "上一页未恢复");
                search.Text = groups[category - 1].Last().Source;
                search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text);
                Check(grid.GetChildCount() == 1 && ((Button)catalog.FindChild("CatalogNext", true, false)).Disabled,
                    "检索未覆盖分类末尾或未重置页码");
                grid.GetChild<Button>(0).EmitSignal(Button.SignalName.Pressed);
                Check(((TextureRect)catalog.FindChild("CatalogPreview", true, false)).Texture is AtlasTexture,
                    "预览没有图像");
                Check(((Label)catalog.FindChild("CatalogDetails", true, false)).Text.Contains(search.Text), "详情来源不符");
                search.Text = "素材不存在-no-such-asset";
                search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text);
                Check(grid.GetChildCount() == 0 && ((Button)catalog.FindChild("CatalogPrevious", true, false)).Disabled
                    && ((Button)catalog.FindChild("CatalogNext", true, false)).Disabled, "空结果仍可翻页");
                search.Text = "";
                search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text);
            }
            GetTree().Root.Size = new Vector2I(1280, 720);
            categories.Select(1);
            categories.EmitSignal(OptionButton.SignalName.ItemSelected, 1);
            grid.GetChild<Button>(0).EmitSignal(Button.SignalName.Pressed);
            await Frames();
            Check(new Rect2(Vector2.Zero, hud.GetViewportRect().Size).Encloses(catalog.GetRect()),
                $"图鉴超出小窗口：viewport={hud.GetViewportRect()} catalog={catalog.GetRect()} minimum={catalog.GetCombinedMinimumSize()}");
            foreach (var name in new[] { "CloseCatalog", "CatalogNext", "CatalogSearch", "CatalogPreview" })
                Check(catalog.GetGlobalRect().Encloses(((Control)catalog.FindChild(name, true, false)).GetGlobalRect()),
                    "操作控件超出图鉴：" + name);
            var before = GameServices.World.ActorState ?? throw new InvalidOperationException("世界未初始化");
            var position = grid.GetChild<Button>(0).GetGlobalRect().GetCenter();
            Input.ParseInputEvent(new InputEventMouseButton { Position = position, GlobalPosition = position,
                ButtonIndex = MouseButton.Left, Pressed = true });
            await Frame();
            Input.ParseInputEvent(new InputEventMouseButton { Position = position, GlobalPosition = position,
                ButtonIndex = MouseButton.Left, Pressed = false });
            await Frames();
            Check(GameServices.World.ActorState?.Position == before.Position, "图鉴点击触发世界移动");
            if (OS.GetCmdlineUserArgs().Contains("--capture-catalog"))
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://build/item-catalog.png"));
                categories.Select(4);
                categories.EmitSignal(OptionButton.SignalName.ItemSelected, 4);
                grid.GetChild<Button>(0).EmitSignal(Button.SignalName.Pressed);
                await Frames();
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://build/weapon-catalog.png"));
            }
            Press(catalog, "CloseCatalog");
            Check(!catalog.Visible, "关闭图鉴失败");
            Press(hud, "OpenItemCatalog");
            Check(catalog.Visible && categories.ItemCount == 5, "重开图鉴重复添加分类");
            main.QueueFree();
            await Frame();
            main = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Frame();
            GD.Print("Item catalog smoke passed: 22872 entries, 4 categories, paging, search, preview, GM, 1280x720, input.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            main?.QueueFree();
            await Frame();
            GetTree().Quit(1);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    /// <summary>通过真实按钮事件执行界面操作。</summary>
    private static void Press(Node root, string name)
    {
        var button = (Button)root.FindChild(name, true, false);
        Check(button is { Disabled: false }, "按钮不可操作：" + name);
        button.EmitSignal(Button.SignalName.Pressed);
    }

    /// <summary>等待一帧完成布局与节点释放。</summary>
    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

    /// <summary>等待协议收发及后续布局。</summary>
    private async Task Frames()
    {
        for (var i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await Frame();
        await Frame();
    }

    /// <summary>以明确异常报告验收失败。</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
