using System;
using System.Collections.Generic;
using System.IO;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Rpg.Net.Message;

namespace Rpg.Net;

/// <summary>显式注册业务协议，统一校验方向并解析 protobuf 信封。</summary>
public static class MessageCodec
{
    /// <summary>允许客户端发送的协议解析器。</summary>
    private static readonly IReadOnlyDictionary<string, MessageParser> Requests = CreateRegistry(
        Login.Descriptor, SelectRole.Descriptor, CreateRole.Descriptor, DeleteRole.Descriptor, Move.Descriptor, Gm.Descriptor, MoveItem.Descriptor, DropItem.Descriptor, PickupItem.Descriptor, ResyncMapAoi.Descriptor);
    /// <summary>允许服务端发送的响应及推送解析器。</summary>
    private static readonly IReadOnlyDictionary<string, MessageParser> Responses = CreateRegistry(
        LoginRt.Descriptor, SelectRoleRt.Descriptor, CreateRoleRt.Descriptor, DeleteRoleRt.Descriptor, MoveRt.Descriptor, RoleStateNotice.Descriptor,
        RoleProfileNotice.Descriptor, RolePositionNotice.Descriptor,
        GmRt.Descriptor, BagChangedNotice.Descriptor, MoveItemRt.Descriptor, DropItemRt.Descriptor, PickupItemRt.Descriptor, MapAoiNotice.Descriptor, ResyncMapAoiRt.Descriptor);

    /// <summary>冻结业务消息为带会话代次的独立字节数组。</summary>
    public static byte[] Encode(IMessage message, long generation, bool request)
    {
        ArgumentNullException.ThrowIfNull(message);
        var registry = request ? Requests : Responses;
        if (!registry.ContainsKey(message.Descriptor.FullName))
            throw new InvalidDataException($"未注册或方向错误的协议：{message.Descriptor.FullName}");
        return new EnvelopePb
        {
            MessageType = message.Descriptor.FullName,
            Generation = generation,
            Payload = message.ToByteString()
        }.ToByteArray();
    }

    /// <summary>解析信封，业务载荷留待会话代次检查之后解析。</summary>
    public static EnvelopePb ReadEnvelope(byte[] bytes)
    {
        try
        {
            var envelope = EnvelopePb.Parser.ParseFrom(bytes);
            if (envelope.Generation <= 0 || string.IsNullOrWhiteSpace(envelope.MessageType))
                throw new InvalidDataException("协议缺少有效类型或会话代次");
            return envelope;
        }
        catch (InvalidProtocolBufferException error) { throw new InvalidDataException("无法解析 protobuf 信封", error); }
    }

    /// <summary>仅使用允许方向的协议表解析业务字节。</summary>
    public static IMessage ReadPayload(EnvelopePb envelope, bool request)
    {
        var registry = request ? Requests : Responses;
        if (!registry.TryGetValue(envelope.MessageType, out var parser))
            throw new InvalidDataException($"未注册或方向错误的协议：{envelope.MessageType}");
        try { return parser.ParseFrom(envelope.Payload); }
        catch (InvalidProtocolBufferException error)
        {
            throw new InvalidDataException($"无法解析协议载荷：{envelope.MessageType}", error);
        }
    }

    /// <summary>以协议完整名称建立明确的解析器映射。</summary>
    private static IReadOnlyDictionary<string, MessageParser> CreateRegistry(params MessageDescriptor[] descriptors)
    {
        var registry = new Dictionary<string, MessageParser>(StringComparer.Ordinal);
        foreach (var descriptor in descriptors) registry.Add(descriptor.FullName, descriptor.Parser);
        return registry;
    }
}
