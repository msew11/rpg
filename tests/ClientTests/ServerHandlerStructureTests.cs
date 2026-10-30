using Rpg.Server.Runtime;
using Rpg.Server.Modules;
using System.Reflection;

namespace ClientTests;

/// <summary>验证 openspec/specs/server-runtime 的单消息 Handler、目录归属和共用逻辑边界。</summary>
public sealed class ServerHandlerStructureTests
{
    /// <summary>每个正式处理器只有一个请求契约，并可按消息名在所属目录直接找到文件。</summary>
    [Fact]
    public void EachHandlerOwnsOneMessageAndDedicatedFile()
    {
        var constructor = Assert.Single(typeof(ServerModules).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.DoesNotContain(constructor.GetParameters(), parameter => parameter.ParameterType.IsGenericType &&
            parameter.ParameterType.GetGenericTypeDefinition().Name.StartsWith("Func`", StringComparison.Ordinal));
        var types = typeof(IActorHandler<,,>).Assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && type.Namespace?.StartsWith("Rpg.Server.Modules.") == true)
            .Select(type => (Type: type, Contracts: type.GetInterfaces().Where(contract =>
                contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IActorHandler<,,>)).ToArray()))
            .Where(entry => entry.Contracts.Length > 0).ToArray();
        Assert.NotEmpty(types);
        foreach (var (type, contracts) in types)
        {
            var contract = Assert.Single(contracts);
            Assert.Equal(contract.GetGenericArguments()[1].Name + "Handler", type.Name);
            Assert.True(type.Namespace!.EndsWith(".Async") || type.Namespace.EndsWith(".Handler"), type.FullName);
            var relative = type.Namespace["Rpg.Server.Modules.".Length..].Replace('.', Path.DirectorySeparatorChar);
            Assert.True(File.Exists(Path.Combine(ConfigTestFixture.FindRoot(), "src", "Server", "Modules", relative, type.Name + ".cs")), type.FullName);
        }

        var modules = Path.Combine(ConfigTestFixture.FindRoot(), "src", "Server", "Modules");
        foreach (var directory in Directory.EnumerateDirectories(modules, "*", SearchOption.AllDirectories)
                     .Where(path => Path.GetFileName(path) is "Handler" or "Async"))
        foreach (var file in Directory.EnumerateFiles(directory, "*.cs"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            Assert.EndsWith("Handler", name);
            Assert.Single(types.Where(entry => entry.Type.Name == name &&
                entry.Type.Namespace == "Rpg.Server.Modules." + Path.GetRelativePath(modules, directory).Replace(Path.DirectorySeparatorChar, '.')));
        }
    }

    /// <summary>本地物品消息归 Handler，跨 Actor 消息归 Async；共用逻辑归 Utils。</summary>
    [Fact]
    public void LocalHandlersAndSharedLogicRespectActorBoundaries()
    {
        var modules = Path.Combine(ConfigTestFixture.FindRoot(), "src", "Server", "Modules");
        foreach (var message in new[] { "Gm", "MoveItem", "GetRoleItems", "PickupItem" })
            Assert.True(File.Exists(Path.Combine(modules, "Role", "Handler", message + "Handler.cs")));
        foreach (var message in new[] { "EquipItem", "DropItem", "CompleteRolePickup", "AdvanceRole", "Move" })
            Assert.True(File.Exists(Path.Combine(modules, "Role", "Async", message + "Handler.cs")));
        Assert.True(File.Exists(Path.Combine(modules, "Common", "Utils", "RoleProjectionSync.cs")));
        foreach (var module in new[] { "Login", "Role", "World" })
        {
            var files = Directory.GetFiles(Path.Combine(modules, module, "Utils"), "*.cs");
            Assert.NotEmpty(files);
            foreach (var file in files)
            {
                var source = File.ReadAllText(file);
                Assert.Contains($"namespace Rpg.Server.Modules.{module}.Utils;", source);
                Assert.DoesNotMatch(@"\bActors\s*\.", source);
                Assert.DoesNotContain("IActorHandler<", source);
            }
        }
    }
}
