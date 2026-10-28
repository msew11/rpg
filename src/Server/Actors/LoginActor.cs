using System;
using System.Threading.Tasks;
using Rpg.Net.Message;
using Rpg.Server.EntityCache;
using Rpg.Server.Handler.Login;
using Rpg.Server.Context;

namespace Rpg.Server.Actors;

/// <summary>启动时唯一创建的登录 Actor，独占全部摘要和列表管理待写结果。</summary>
internal sealed class LoginActor : BaseActor
{
    /// <summary>只负责实体文件读写。</summary>
    private readonly RepoServerRole _repo;
    /// <summary>本 Actor 独占的登录缓存。</summary>
    private readonly ServerLoginCache _cache = new();
    /// <summary>处理器使用的只读规则。</summary>
    private readonly DataServerRules _rules;

    /// <summary>本 Actor 复用的Login请求处理器。</summary>
    private readonly LoginHandler _loginHandler = new();
    /// <summary>本 Actor 复用的CreateRole请求处理器。</summary>
    private readonly CreateRoleHandler _createRoleHandler = new();
    /// <summary>本 Actor 复用的DeleteRole请求处理器。</summary>
    private readonly DeleteRoleHandler _deleteRoleHandler = new();

    /// <summary>绑定存储和配置；初始化由首条消息触发。</summary>
    internal LoginActor(RepoServerRole repo, DataServerRules rules)
    { _repo = repo; _rules = rules; }

    /// <summary>完整加载摘要，不读取任何角色物品表。</summary>
    protected override ValueTask InitializeAsync()
    {
        foreach (var summary in _repo.ReadSummaries()) _cache.Summaries.Add(summary.RoleId, summary);
        _cache.Loaded = true;
        return ValueTask.CompletedTask;
    }

    /// <summary>只在 RUN 状态管理角色摘要和未落地创建/删除。</summary>
    protected override ValueTask<object?> ReceiveAsync(object message)
    {
        var context = new LoginContext(_cache, _rules);
        object? response = message switch
        {
            DataInitialize => true,
            DataSummaryChanged changed => Update(changed),
            DataClaimRole claim => Claim(claim.RoleId),
            DataLoginWork work => work.Work(context),
            DataLoginRequest request => Handle(context, request),
            _ => throw new InvalidOperationException("登录 Actor 不支持此消息")
        };
        return ValueTask.FromResult(response);
    }

    /// <summary>验证列表身份后准备删除单例，再路由到纯内存 Handler。</summary>
    private object Handle(LoginContext context, DataLoginRequest message)
    {
        context.Authenticated = message.Authenticated;
        context.HasSelectedRole = message.Selected;
        if (message.Request is Login login) return _loginHandler.Handle(context, login);
        if (message.Selected) throw new InvalidOperationException("请在登录后的角色列表操作");
        if (message.Request is DeleteRole delete && message.Authenticated &&
            !_cache.Created.ContainsKey(delete.RoleId) && !_cache.Deletions.ContainsKey(delete.RoleId) &&
            _cache.Summaries.TryGetValue(delete.RoleId, out var summary) && !summary.IsDeleted)
            _cache.Deletions[delete.RoleId] = _repo.ReadRole(delete.RoleId);
        return message.Request switch
        {
            CreateRole create => _createRoleHandler.Handle(context, create),
            DeleteRole removal => _deleteRoleHandler.Handle(context, removal),
            _ => throw new InvalidOperationException("登录消息类型无效")
        };
    }

    /// <summary>发布新摘要，不通过回调访问其他 Actor。</summary>
    private bool Update(DataSummaryChanged message) { _cache.Summaries[message.Summary.RoleId] = message.Summary; return true; }

    /// <summary>初始化角色前检查摘要身份并移交新建实体的所有权。</summary>
    private DataRoleSeed Claim(long id)
    {
        if (!_cache.Summaries.TryGetValue(id, out var summary) || summary.IsDeleted)
            throw new InvalidOperationException("角色不存在或已删除");
        _cache.Created.TryGetValue(id, out var created);
        return new(created);
    }

}
