using System;
using System.Net;

namespace Rpg.Net;

/// <summary>一次登录选择的不可变连接配置。</summary>
public sealed record ConnectionOptions(bool HostServer, string Ip = "127.0.0.1", int Port = 7777)
{
    /// <summary>校验界面输入，不接受主机名或无效端口。</summary>
    public void Validate()
    {
        if (Port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(Port), "端口必须为 1–65535");
        if (!HostServer && !IPAddress.TryParse(Ip, out _)) throw new ArgumentException("请输入合法 IP 地址", nameof(Ip));
    }
}
