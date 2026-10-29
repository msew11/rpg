using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Rpg.ServerV2.Modules.Common.Cache;

/// <summary>统一默认数组为空及数组元素的内容比较。</summary>
public abstract class DataCacheArray<T> : DataCache<ImmutableArray<T>>
{
    /// <summary>元素的内容比较器，可由具体数据类型提供。</summary>
    private readonly IEqualityComparer<T> _comparer;
    /// <summary>加载数组时先规范化，不将默认数组保留为运行状态。</summary>
    protected DataCacheArray(ImmutableArray<T> value, IEqualityComparer<T>? comparer = null)
        : base(value.IsDefault ? [] : value) => _comparer = comparer ?? EqualityComparer<T>.Default;
    /// <summary>所有提交入口统一接纳默认数组为空。</summary>
    protected override ImmutableArray<T> Normalize(ImmutableArray<T> value) => value.IsDefault ? [] : value;
    /// <summary>按元素顺序比较，避免重新构造数组引起误写。</summary>
    protected override bool ContentEquals(ImmutableArray<T> left, ImmutableArray<T> right) => left.SequenceEqual(right, _comparer);
}
