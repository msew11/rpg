using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Rpg.Const;
using Rpg.Server.Modules.Role.Storage;
using Rpg.Server.Modules.Role.Data;
using Rpg.Services;
using Rpg.Tests.Doubles;
using Rpg.UI;

namespace Rpg.Tests;

/// <summary>验收 openspec/specs/client-item-assets 的选角预热与首次开包，输出实际主线程耗时。</summary>
public partial class ItemAssetSmoke : Node
{
    /// <summary>使用隔离存档进入真实世界并比较首次和再次开包，不使用硬件相关阈值。</summary>
    public override async void _Ready()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rpg-item-assets-" + Guid.NewGuid());
        DoubleMainController? main = null;
        try
        {
            var serverDirectory = Path.Combine(directory, "server");
            RoleTestData.Seed(serverDirectory);
            var storage = new RoleStorage(serverDirectory);
            RoleTestData.SetItems(serverDirectory, 1001, Enumerable.Concat(Enumerable.Range(1013, 6).Select((id, index) => new DataItem(index + 1, id, index, 1)).ToArray(), [new DataItem(7, 1012, EquipmentSlot.MainHand)]), 8);
            main = new DoubleMainController { SettingsDirectory = Path.Combine(directory, "settings"),
                ServerDirectory = serverDirectory, EnvFilePath = Path.Combine(directory, ".env") };
            AddChild(main);
            GameServices.Session.Login("demo1", "123");
            await Until(() => GameServices.Session.IsLoggedIn);
            var selection = Stopwatch.StartNew();
            ((Button)main.FindChild("SelectRole1001", true, false)).EmitSignal(Button.SignalName.Pressed);
            ((Button)main.FindChild("EnterRole", true, false)).EmitSignal(Button.SignalName.Pressed);
            await Until(() => GameServices.World.HasRole);
            selection.Stop();
            Check(GameServices.World.HasRole, "选角未建立世界");
            var assets = GameServices.Items;
            var iconIds = GameServices.Config.Item.Items.Values.Select(item => item.IconAssetId).Distinct().ToArray();
            Check(assets.CatalogLoadCount == 1 && assets.CachedTextureCount == iconIds.Length,
                "选角后未准备完整背包与装备图标");
            var catalog = assets.Catalog;
            var atlasCount = assets.LoadedAtlasCount;
            Check(atlasCount == iconIds.Select(id => assets.Index[id].Atlas).Distinct().Count(), "预热加载了无关图集");
            var textures = iconIds.Select(assets.Resolve).ToArray();
            Check(textures.All(texture => texture is not null), "预热缺少有效纹理");
            await Frames();
            var bag = main.FindChildren("*", "Control", true, false).OfType<BagControl>().Single();
            var first = OpenBag(bag);
            await Frames();
            var grid = (GridContainer)bag.FindChild("BagGrid", true, false);
            Check(grid.GetChildCount() == 40, "背包格子数量不正确");
            Check(grid.FindChildren("*", "TextureRect", true, false).OfType<TextureRect>()
                .Count(icon => icon.Texture is AtlasTexture) == 6, "背包物品图标未完整显示");
            bag.Hide();
            var second = OpenBag(bag);
            Check(assets.CatalogLoadCount == 1 && assets.LoadedAtlasCount == atlasCount &&
                assets.CachedTextureCount == iconIds.Length, "首次或再次开包重新加载了物品资源");
            Check(textures.SequenceEqual(iconIds.Select(assets.Resolve)), "开包没有复用预热纹理");
            GD.Print($"ITEM_ASSETS selection_ms={selection.Elapsed.TotalMilliseconds:F2} first_open_ms={first:F2} reopen_ms={second:F2} renderer={DisplayServer.GetName()}");
            GD.Print($"ITEM_ASSETS catalog_ms={assets.CatalogLoadMilliseconds:F2} textures_ms={assets.TextureLoadMilliseconds:F2} catalogs={assets.CatalogLoadCount} atlases={atlasCount}");
            main.FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(button => button.Text == "返回角色选择").EmitSignal(Button.SignalName.Pressed);
            await Frames();
            ((Button)main.FindChild("SelectRole1002", true, false)).EmitSignal(Button.SignalName.Pressed);
            ((Button)main.FindChild("EnterRole", true, false)).EmitSignal(Button.SignalName.Pressed);
            await Frames();
            Check(GameServices.Session.RoleId == 1002 && GameServices.World.HasRole, "空包角色未成功进入");
            Check(ReferenceEquals(catalog, assets.Catalog) && assets.CatalogLoadCount == 1 &&
                assets.LoadedAtlasCount == atlasCount, "切换空包角色重新解析目录或加载无关图集");
            var emptyBag = main.FindChildren("*", "Control", true, false).OfType<BagControl>().Single();
            OpenBag(emptyBag);
            Check(GameServices.Session.Bag.Items.Count == 0, "切角残留上一角色物品");
            RemoveChild(main);
            main.Free();
            main = null;
            Check(assets.IsDisposed && assets.LoadedAtlasCount == 0, "退出未清空物品缓存");
            GD.Print("Item assets passed.");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
        finally
        {
            if (main is not null) { RemoveChild(main); main.Free(); }
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    /// <summary>测量可见性回调内的完整首次网格创建和图标绑定。</summary>
    private static double OpenBag(BagControl bag)
    {
        var clock = Stopwatch.StartNew();
        bag.Show();
        return clock.Elapsed.TotalMilliseconds;
    }

    /// <summary>等待模拟登录，有截止时间避免启动失败无限等待。</summary>
    private async Task Until(Func<bool> predicate)
    {
        var clock = Stopwatch.StartNew();
        while (!predicate())
        {
            if (clock.Elapsed.TotalSeconds > 15) throw new TimeoutException("等待角色流程超时");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    /// <summary>推进布局和延迟删除，使开包比较使用相同条件。</summary>
    private async Task Frames()
    {
        await SmokeNetwork.WaitForAccount(this);
        for (var index = 0; index < 4; index++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    /// <summary>功能失败以异常中止自动验收。</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
