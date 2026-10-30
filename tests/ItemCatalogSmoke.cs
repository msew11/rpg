using Rpg.Utils;
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

/// <summary>验收 openspec/specs/item-catalog 的具名两级分类、缺图重复ID及原素材模式的真实 GM 交互。</summary>
public partial class ItemCatalogSmoke : Node
{
    /// <summary>隔离存档并自动操作正式 HUD，以退出码报告回归结果。</summary>
    public override async void _Ready()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rpg-catalog-" + Guid.NewGuid());
        DoubleMainController? main = null;
        try
        {
            var entries = ItemAtlasUtil.LoadCatalog();
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
            RoleTestData.Seed(main.ServerDirectory);
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
            var mode = (OptionButton)catalog.FindChild("CatalogMode", true, false);
            Check(mode.Selected == 0 && categories.ItemCount == 7, "默认未展示六父类型道具清单");
            CheckDefinitions(catalog, entries);
            Select(mode, 1);
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
            foreach (var name in new[] { "CloseCatalog", "CatalogNext", "CatalogSearch", "CatalogPreview", "CatalogMode" })
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
            Select(mode, 0);
            Select(categories, 1);
            var subtypes = (OptionButton)catalog.FindChild("CatalogSubtype", true, false);
            Select(subtypes, Enumerable.Range(1, subtypes.ItemCount - 1).Single(index => subtypes.GetItemText(index).StartsWith("剑（")));
            grid.GetChild<Button>(0).EmitSignal(Button.SignalName.Pressed);
            await Frames();
            foreach (var name in new[] { "CatalogCategory", "CatalogSubtype", "CatalogSearch", "CloseCatalog", "CatalogPreview" })
                Check(catalog.GetGlobalRect().Encloses(((Control)catalog.FindChild(name, true, false)).GetGlobalRect()),
                    "两级分类控件超出小窗口：" + name);
            await Capture("item-catalog-classified.png");
            Select(categories, 2);
            Select(subtypes, Enumerable.Range(1, subtypes.ItemCount - 1).Single(index => subtypes.GetItemText(index).StartsWith("项链（")));
            grid.GetChild<Button>(0).EmitSignal(Button.SignalName.Pressed);
            await Frames();
            await Capture("item-catalog-necklaces.png");
            Press(catalog, "CloseCatalog");
            Check(!catalog.Visible, "关闭图鉴失败");
            Press(hud, "OpenItemCatalog");
            Check(catalog.Visible && categories.ItemCount == 7, "重开图鉴重复添加分类");
            main.QueueFree();
            await Frame();
            main = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Frame();
            GD.Print("Item catalog smoke passed: 21792 definitions, 6 parent categories and all subtypes, duplicates, missing icons, 22872 assets, GM, 1280x720, input.");
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

    /// <summary>核对六大类与全部子类型、名称ID说明检索、重复行和缺图预览隔离。</summary>
    private static void CheckDefinitions(ItemCatalogControl catalog, DataItemAsset[] assets)
    {
        var items = JsonSerializer.Deserialize<DataItemDefinition[]>(FileAccess.GetFileAsString("res://assets/items/definitions.json"))!;
        Check(items.Length == 21792 && items.Select(item => item.Key).Distinct().Count() == 21792, "清单丢失来源行");
        Check(items.Count(item => item.IconAssetId == "") == 477, "缺图道具未完整保留");
        foreach (var id in new[] { 13541, 13542, 8014 })
            Check(items.Count(item => item.ItemId == id) == 2, "重复ID被合并：" + id);
        var assetById = assets.ToDictionary(asset => asset.Id);
        foreach (var item in items.Where(item => item.IconAssetId != ""))
            Check(assetById[item.IconAssetId].Source == item.IconCandidate.Replace("../art_export/", ""), "候选路径被替换成道具ID猜图");
        var parents = new[] { "武器", "防具／饰品", "其他装备", "宠物", "收集品", "消耗品" };
        var counts = new[] { 879, 8286, 30, 1, 1073, 11523 };
        var categories = (OptionButton)catalog.FindChild("CatalogCategory", true, false);
        var subtypes = (OptionButton)catalog.FindChild("CatalogSubtype", true, false);
        var search = (LineEdit)catalog.FindChild("CatalogSearch", true, false);
        var grid = (GridContainer)catalog.FindChild("CatalogGrid", true, false);
        var status = (Label)catalog.FindChild("CatalogStatus", true, false);
        var details = (Label)catalog.FindChild("CatalogDetails", true, false);
        var preview = (TextureRect)catalog.FindChild("CatalogPreview", true, false);
        var itemByKey = items.ToDictionary(item => item.Key);
        for (var parent = 0; parent < parents.Length; parent++)
        {
            Select(categories, parent + 1);
            Check(subtypes.Selected == 0 && categories.GetItemText(parent + 1).Contains(counts[parent].ToString("N0")),
                "父类型数量错误或未重置子类型");
            Check(status.Text.Contains(counts[parent].ToString("N0")), "父类型实际结果数不符");
            var groups = items.Where(item => item.Category == parents[parent]).GroupBy(item => item.TypeCode).OrderBy(group => group.Key).ToArray();
            Check(subtypes.ItemCount == groups.Length + 1, "子类型缺失");
            for (var subtype = 0; subtype < groups.Length; subtype++)
            {
                Select(subtypes, subtype + 1);
                Check(subtypes.GetItemText(subtype + 1).StartsWith(groups[subtype].First().TypeName)
                    && status.Text.Contains(groups[subtype].Count().ToString("N0")), "子类型名称或数量不符");
                Check(grid.GetChildCount() == Math.Min(48, groups[subtype].Count()), "子类型分页数量不符");
                foreach (var button in grid.GetChildren().OfType<Button>())
                {
                    var item = itemByKey[button.GetMeta("entry_key").AsString()];
                    Check(item.Category == parents[parent] && item.TypeCode == groups[subtype].Key, "子类型筛选混入其他父类型");
                }
            }
        }
        Select(categories, 1);
        var firstKey = grid.GetChild<Button>(0).GetMeta("entry_key").AsString();
        Press(catalog, "CatalogNext");
        Check(firstKey != grid.GetChild<Button>(0).GetMeta("entry_key").AsString(), "道具下一页未更新");
        Press(catalog, "CatalogPrevious");
        Check(firstKey == grid.GetChild<Button>(0).GetMeta("entry_key").AsString(), "道具上一页未恢复");
        Query(search, "铁剑");
        var iron = grid.GetChildren().OfType<Button>().Single(button => button.GetMeta("entry_key").AsString() == "ov_arm.bin:1");
        iron.EmitSignal(Button.SignalName.Pressed);
        Check(preview.Texture is AtlasTexture icon && icon.Region == new Rect2(assetById["item/itempic/itempic1001"].X,
            assetById["item/itempic/itempic1001"].Y, assetById["item/itempic/itempic1001"].W,
            assetById["item/itempic/itempic1001"].H), "铁剑图标候选未精确匹配");
        Check(details.Text.Contains("铁剑") && details.Text.Contains("按装备字段推定"), "具名详情缺少分类状态");
        Query(search, "完全不存在的道具-no-such-item");
        Check(grid.GetChildCount() == 0 && ((Button)catalog.FindChild("CatalogNext", true, false)).Disabled,
            "道具空结果仍可翻页");
        Query(search, "");
        Select(categories, 2);
        Query(search, "24618");
        var necklace = grid.GetChildren().OfType<Button>().Single(button => button.GetMeta("entry_key").AsString() == "ov_arm.bin:4702");
        necklace.EmitSignal(Button.SignalName.Pressed);
        Check(details.Text.Contains("项链") && !details.Text.Contains("武器 /"), "项链仍使用旧区间0/1错误分类");
        Query(search, "");
        Select(categories, 6);
        Query(search, "阿狸抱枕");
        foreach (var duplicate in items.Where(item => item.ItemId == 13541))
            Check(grid.GetChildren().OfType<Button>().Any(button => button.GetMeta("entry_key").AsString() == duplicate.Key),
                "同ID道具行未分别显示");
        Query(search, "");
        Select(categories, 0);
        Query(search, "5秒内恢复生命150");
        Check(grid.GetChildren().OfType<Button>().Any(button => button.GetMeta("entry_key").AsString() == "ov_item.bin:1"), "说明搜索未找到肉片");
        Query(search, "");
        var missing = items.First(item => item.IconAssetId == "");
        Select(categories, Array.IndexOf(parents, missing.Category) + 1);
        Query(search, missing.ItemId.ToString());
        var missingButton = grid.GetChildren().OfType<Button>().Single(button => button.GetMeta("entry_key").AsString() == missing.Key);
        missingButton.EmitSignal(Button.SignalName.Pressed);
        Check(preview.Texture is null && ((Label)catalog.FindChild("CatalogMissingIcon", true, false)).Visible
            && details.Text.Contains(missing.Name), "缺图记录沿用了旧预览或丢失详情");
        Query(search, "");
        Select(categories, 0);
    }

    /// <summary>按用户操作发出分类选择信号。</summary>
    private static void Select(OptionButton button, int index)
    {
        button.Select(index);
        button.EmitSignal(OptionButton.SignalName.ItemSelected, index);
    }

    /// <summary>模拟用户输入，触发搜索更新。</summary>
    private static void Query(LineEdit search, string value)
    {
        search.Text = value;
        search.EmitSignal(LineEdit.SignalName.TextChanged, value);
    }

    /// <summary>可选输出分类道具页面，供真实图形布局复核。</summary>
    private async Task Capture(string name)
    {
        if (!OS.GetCmdlineUserArgs().Contains("--capture-catalog")) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://build/" + name));
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
        await SmokeNetwork.WaitForAccount(this);
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
