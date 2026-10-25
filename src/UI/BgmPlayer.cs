using System;
using Godot;

namespace Rpg.UI;

/// <summary>在页面与地图间复用一条循环背景音乐，并使用 BGM 音量总线。</summary>
public partial class BgmPlayer : Node
{
    /// <summary>项目内背景音乐资源目录。</summary>
    private const string MusicDirectory = "res://assets/audio/music/";
    /// <summary>本场景唯一的背景音乐播放节点。</summary>
    private readonly AudioStreamPlayer _player = new() { Name = "Music", Bus = "BGM" };
    /// <summary>当前正在播放的曲目编号，空值表示静音。</summary>
    internal string? CurrentTrackId { get; private set; }

    /// <summary>建立播放器，BGM 总线由同场景的界面音效节点预先创建。</summary>
    public override void _Ready() => AddChild(_player);

    /// <summary>切换曲目；同曲保持进度，空编号停止播放。</summary>
    public void PlayTrack(string? trackId)
    {
        if (string.IsNullOrEmpty(trackId)) { Stop(); return; }
        if (CurrentTrackId == trackId) return;
        var path = $"{MusicDirectory}{trackId}.ogg";
        var stream = ResourceLoader.Load<AudioStreamOggVorbis>(path)
            ?? throw new InvalidOperationException($"无法加载背景音乐：{path}");
        stream.Loop = true;
        _player.Stop();
        _player.Stream = stream;
        // Headless 没有音频输出，跳过实际播放以免退出时遗留流式解码对象。
        if (DisplayServer.GetName() != "headless") _player.Play();
        CurrentTrackId = trackId;
    }

    /// <summary>停止当前曲目并释放播放器持有的资源。</summary>
    public void Stop()
    {
        _player.Stop();
        _player.Stream = null;
        CurrentTrackId = null;
    }

    /// <summary>主场景退出时关闭背景音乐。</summary>
    public override void _ExitTree() => Stop();
}
