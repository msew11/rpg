using System.Collections.Immutable;
using Rpg.ServerV2.Runtime;

namespace Rpg.ServerV2.Modules.Login.Data;

/// <summary>规范账号已经由路由确定，邮箱内只接收独立的凭据数据。</summary>
/// <param name="Password">后续认证处理器所需的密码，不进入诊断文本。</param>
public sealed record DataLogin(string Password) : IActorRequest<DataLoginResult>;

/// <summary>内部成功结果，失败通过类型化框架错误返回。</summary>
/// <param name="Roles">该账号可展示的不可变摘要集合。</param>
public sealed record DataLoginResult(ImmutableArray<DataRoleSummary> Roles);
