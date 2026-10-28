using System;
using System.Collections;
using System.Runtime.CompilerServices;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace Rpg.Net;

/// <summary>关联解码消息与信封版本，不向业务 protobuf 或存档添加字段。</summary>
public static class MessageRevision
{
    /// <summary>只随消息对象存活的进程内接收元数据。</summary>
    private static readonly ConditionalWeakTable<IMessage, DataRevision> Revisions = new();

    /// <summary>读取消息来源信封的版本；本地未标记数据使用初始零值。</summary>
    public static long GetRevision(this IMessage message) =>
        Revisions.TryGetValue(message, out var value) ? value.Revision : 0;

    /// <summary>标记消息及嵌套载荷，使各数据转换保持相同信封版本。</summary>
    public static T WithRevision<T>(this T message, long revision) where T : class, IMessage
    {
        ArgumentNullException.ThrowIfNull(message);
        Revisions.Remove(message);
        Revisions.Add(message, new DataRevision(revision));
        foreach (var field in message.Descriptor.Fields.InFieldNumberOrder())
        {
            if (field.FieldType != FieldType.Message) continue;
            var value = field.Accessor.GetValue(message);
            if (value is IMessage child) child.WithRevision(revision);
            else if (value is IEnumerable children)
                foreach (var item in children)
                    if (item is IMessage nested) nested.WithRevision(revision);
        }
        return message;
    }

    /// <summary>保存不可变的单次信封版本。</summary>
    /// <param name="Revision">发送端消息序号。</param>
    private sealed record DataRevision(long Revision);
}
