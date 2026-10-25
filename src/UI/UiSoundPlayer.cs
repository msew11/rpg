using Godot;
using System.Collections.Generic;

namespace Rpg.UI;

/// <summary>播放界面与道具音效，并为动态创建的按钮和页签绑定反馈。</summary>
public partial class UiSoundPlayer : Node
{
    /// <summary>音效资源所在目录。</summary>
    private const string SoundDirectory = "res://assets/audio/effects/";
    /// <summary>场景生命周期内复用的六个短音效播放器。</summary>
    private readonly Dictionary<int, AudioStreamPlayer> _players = new();

    /// <summary>建立独立音量总线并监听后续创建的控件。</summary>
    public override void _Ready()
    {
        EnsureBus("BGM");
        EnsureBus("SFX");
        foreach (var id in new[] { 1001, 1003, 1005, 1006, 1007, 1010 })
        {
            var stream = ResourceLoader.Load<AudioStream>($"{SoundDirectory}{id}.wav");
            if (stream is null) continue;
            var player = new AudioStreamPlayer { Stream = stream, Bus = "SFX" };
            AddChild(player);
            _players.Add(id, player);
        }
        GetTree().NodeAdded += OnNodeAdded;
    }

    /// <summary>解除节点监听，避免场景重开时重复播放。</summary>
    public override void _ExitTree()
    {
        GetTree().NodeAdded -= OnNodeAdded;
        foreach (var player in _players.Values)
        {
            player.Stop();
            player.Stream = null;
        }
        _players.Clear();
    }

    /// <summary>应用已保存或试听中的两路音量。</summary>
    public static void SetVolumes(float bgm, float effects)
    {
        AudioServer.SetBusVolumeLinear(EnsureBus("BGM"), Mathf.Clamp(bgm, 0, 1));
        AudioServer.SetBusVolumeLinear(EnsureBus("SFX"), Mathf.Clamp(effects, 0, 1));
    }

    /// <summary>按编号播放一次短音效。</summary>
    public static void Play(Node source, int id)
    {
        // Headless 测试没有音频输出，避免退出时与音频线程争用 WAV 播放资源。
        if (DisplayServer.GetName() == "headless") return;
        var host = source.GetTree().Root.FindChild("UiSound", true, false);
        if (host is not UiSoundPlayer sound) return;
        if (!sound._players.TryGetValue(id, out var player)) return;
        player.Stop();
        player.Play();
    }

    /// <summary>按控件用途选择菜单、取消或确认反馈。</summary>
    private void OnNodeAdded(Node node)
    {
        if (node is TabContainer tabs)
            tabs.TabChanged += _ => Play(this, 1003);
        if (node is not Button button) return;
        button.Pressed += () =>
        {
            if (button.Name.ToString().StartsWith("SelectRole") || button.Text == "系统" ||
                button.Text is "返回角色选择" or "角色设置" or "系统设置")
                Play(this, 1003);
            else if (button.Text.Contains("取消") || button.Text.Contains("关闭") || button.Text.Contains("退出") ||
                     button.Text.StartsWith("返回") ||
                     button.Name.ToString().StartsWith("Cancel"))
                Play(this, 1007);
            else if (button.Text is not "登录" and not "进入游戏")
                Play(this, 1006);
        };
    }

    /// <summary>在没有项目音频总线配置时创建业务总线。</summary>
    private static int EnsureBus(string name)
    {
        var index = AudioServer.GetBusIndex(name);
        if (index >= 0) return index;
        AudioServer.AddBus();
        index = AudioServer.BusCount - 1;
        AudioServer.SetBusName(index, name);
        return index;
    }
}
