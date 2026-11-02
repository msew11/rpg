using System;
using System.Collections.Generic;
using System.Collections.Frozen;
using System.IO;
using Rpg.Protocol;

namespace Rpg.Session.Data;

/// <summary>与协议对象分离的只读正常属性快照。</summary>
public sealed record AttributesPbData
{
    /// <summary>按公共属性编号索引的实际数值。</summary>
    public IReadOnlyDictionary<int, double> Values { get; }
    /// <summary>仅用于尚未从协议初始化的本地快照。</summary>
    public static AttributesPbData Empty { get; } = new(FrozenDictionary<int, double>.Empty);
    /// <summary>接管已经冻结的集合。</summary>
    private AttributesPbData(IReadOnlyDictionary<int, double> values) => Values = values;
    /// <summary>验证完整快照并复制，未知正编号保留以支持扩展。</summary>
    public static AttributesPbData FromPb(AttributesPb message)
    {
        if (message is null) throw new InvalidDataException("缺少正常属性");
        foreach (var pair in message.Values)
            if (pair.Key <= 0 || !double.IsFinite(pair.Value)) throw new InvalidDataException("属性编号或数值无效");
        for (var id = 1; id <= 6; id++)
            if (!message.Values.ContainsKey(id)) throw new InvalidDataException("正常属性不完整");
        return new(message.Values.ToFrozenDictionary());
    }
    /// <summary>生成独立的完整协议对象。</summary>
    public AttributesPb ToPb() { var result = new AttributesPb(); foreach (var pair in Values) result.Values.Add(pair.Key, pair.Value); return result; }
}

/// <summary>角色存档恢复的 GM 移速设置，独立于正常属性。</summary>
public sealed record GmMovementSpeedPbData(bool Enabled, double Speed)
{
    /// <summary>拒绝非有限和负数设置。</summary>
    public static GmMovementSpeedPbData FromPb(GmMovementSpeedPb message)
    {
        if (message is null || !double.IsFinite(message.Speed) || message.Speed < 0) throw new InvalidDataException("GM 移速设置无效");
        return new(message.Enabled, message.Speed);
    }
    /// <summary>生成独立的设置协议。</summary>
    public GmMovementSpeedPb ToPb() => new() { Enabled = Enabled, Speed = Speed };
}
