using System;
using Rpg.ProtocolV2;

namespace Rpg.ServerV2.Protocol;

/// <summary>可以映射为统一 ErrorRt 的明确请求失败。</summary>
public sealed class ServerRequestException : Exception
{
    /// <summary>可供调用方分支处理的错误码。</summary>
    public ErrorCodePb Code { get; }
    /// <summary>构造可读且不包含凭据的请求错误。</summary>
    public ServerRequestException(ErrorCodePb code, string message) : base(message) => Code = code;
}
