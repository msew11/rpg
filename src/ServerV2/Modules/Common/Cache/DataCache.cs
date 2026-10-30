using System;
using System.Collections.Generic;

namespace Rpg.ServerV2.Modules.Common.Cache;

/// <summary>所属邮箱独占的数据工作区，管理当前值与成功保存基线。</summary>
public abstract class DataCache<T>
{
    /// <summary>当前不可变数据值，仅通过提交替换。</summary>
    public T Value { get; private set; }
    /// <summary>最近加载或成功保存的数据值。</summary>
    protected T Saved { get; private set; }
    /// <summary>按数据内容判断是否需要写回。</summary>
    public bool IsDirty => !_hasSaved || !ContentEquals(Value, Saved);
    /// <summary>首次建档尚无落盘基线，空表也必须保存。</summary>
    private bool _hasSaved = true;
    /// <summary>仅在首次建立候选数据时撤销保存基线。</summary>
    internal void MarkUnsaved() => _hasSaved = false;
    /// <summary>用已规范化的完整加载值建立基线，不调用虚方法。</summary>
    protected DataCache(T value) { ArgumentNullException.ThrowIfNull(value); Value = value; Saved = value; }
    /// <summary>回合内替换当前值，失败不改变基线。</summary>
    public void Commit(T value) { ArgumentNullException.ThrowIfNull(value); Value = Normalize(value); }
    /// <summary>规范化外部提交值；具体业务校验归所属数据缓存。</summary>
    protected virtual T Normalize(T value) => value;
    /// <summary>默认按值比较，嵌套数组或分块数据可覆盖。</summary>
    protected virtual bool ContentEquals(T left, T right) => EqualityComparer<T>.Default.Equals(left, right);
    /// <summary>只在所属 Actor 确认仓库保存成功后更新基线。</summary>
    internal void AcceptSaved() => AcceptSaved(Value);
    /// <summary>只接受已实际保存的捕获值，保存期间的后续提交仍待写。</summary>
    internal void AcceptSaved(T value) { Saved = Normalize(value); _hasSaved = true; }
}
