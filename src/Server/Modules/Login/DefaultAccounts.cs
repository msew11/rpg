namespace Rpg.Server.Modules.Login;

/// <summary>开发期允许登录的固定账号，不属于正式认证系统。</summary>
public static class DefaultAccounts
{
    /// <summary>服务器模式唯一允许的本机登录账号。</summary>
    public const string Administrator = "admin";
    /// <summary>纯客户端初始账号。</summary>
    public const string Client = "demo1";
    /// <summary>开发期默认密码，原样匹配。</summary>
    public const string Password = "123";
    /// <summary>校验规范账号键与原样密码。</summary>
    public static bool Accepts(string username, string password) => password == Password && username is Administrator or Client or "demo2";
}
