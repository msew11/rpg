namespace Rpg.Server.Data;

/// <summary>从启动配置复制的服务端道具规则，不引用客户端配置对象。</summary>
/// <param name="Id">道具配置编号。</param>
/// <param name="Name">反馈中的道具名称。</param>
/// <param name="MaxStack">单格最大数量。</param>
public sealed record DataServerItemDefinition(int Id, string Name, int MaxStack);
