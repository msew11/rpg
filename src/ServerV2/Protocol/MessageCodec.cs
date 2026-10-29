using System;
using System.Collections.Generic;
using System.IO;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Rpg.ProtocolV2;

namespace Rpg.ServerV2.Protocol;

/// <summary>只处理 V2 字节、类型和方向，不承担账号或 Actor 路由。</summary>
public static class MessageCodec
{
    /// <summary>显式请求协议表；摘要数据类型不作为可投递消息。</summary>
    private static readonly IReadOnlyDictionary<string, MessageDescriptor> Requests = Registry(Login.Descriptor, CreateRole.Descriptor, DeleteRole.Descriptor);
    /// <summary>显式响应协议表。</summary>
    private static readonly IReadOnlyDictionary<string, MessageDescriptor> Responses = Registry(LoginRt.Descriptor, CreateRoleRt.Descriptor, DeleteRoleRt.Descriptor, ErrorRt.Descriptor);
    /// <summary>主动通知协议表，本版尚无游戏通知。</summary>
    private static readonly IReadOnlyDictionary<string, MessageDescriptor> Notices = Registry();

    /// <summary>解析信封基础结构；业务载荷在代次检查后才解析。</summary>
    public static EnvelopePb ReadEnvelope(byte[] bytes)
    {
        try
        {
            var envelope = EnvelopePb.Parser.ParseFrom(bytes);
            if (envelope.Generation == 0 || string.IsNullOrWhiteSpace(envelope.MessageType))
                throw new InvalidDataException("V2 信封缺少有效代次或类型");
            return envelope;
        }
        catch (InvalidProtocolBufferException error) { throw new InvalidDataException("无法解析 V2 信封", error); }
    }

    /// <summary>验证发送方向和编号，再解析新建的 protobuf 实例。</summary>
    public static IMessage ReadPayload(EnvelopePb envelope, bool request)
    {
        var descriptor = Resolve(envelope, request);
        try { return descriptor.Parser.ParseFrom(envelope.Payload); }
        catch (InvalidProtocolBufferException) { throw new ServerRequestException(ErrorCodePb.InvalidRequest, "无法解析 V2 协议载荷"); }
    }

    /// <summary>查询类型与方向契约，不重复解析已由发送方持有的消息。</summary>
    private static MessageDescriptor Resolve(EnvelopePb envelope, bool request)
    {
        var registry = request ? Requests : envelope.RequestId == 0 ? Notices : Responses;
        if (request && envelope.RequestId == 0)
            throw new ServerRequestException(ErrorCodePb.InvalidRequest, "请求编号必须非零");
        if (!registry.TryGetValue(envelope.MessageType, out var descriptor))
        {
            var known = Requests.ContainsKey(envelope.MessageType) || Responses.ContainsKey(envelope.MessageType) || Notices.ContainsKey(envelope.MessageType);
            throw new ServerRequestException(known ? ErrorCodePb.WrongDirection : ErrorCodePb.UnknownMessage,
                known ? "V2 协议发送方向错误" : "未注册的 V2 协议类型");
        }
        return descriptor;
    }

    /// <summary>冻结消息为独立字节，响应和通知通过编号区分。</summary>
    public static byte[] Encode(IMessage message, ulong generation, ulong requestId, bool request)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (generation == 0) throw new ArgumentOutOfRangeException(nameof(generation));
        var envelope = new EnvelopePb
        { Generation = generation, RequestId = requestId, MessageType = message.Descriptor.FullName, Payload = message.ToByteString() };
        Resolve(envelope, request);
        return envelope.ToByteArray();
    }

    /// <summary>以完整类型名构建显式解析器表。</summary>
    private static IReadOnlyDictionary<string, MessageDescriptor> Registry(params MessageDescriptor[] descriptors)
    {
        var registry = new Dictionary<string, MessageDescriptor>(StringComparer.Ordinal);
        foreach (var descriptor in descriptors) registry.Add(descriptor.FullName, descriptor);
        return registry;
    }
}
