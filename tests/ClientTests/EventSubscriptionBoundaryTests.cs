using Rpg.Config;
using System.Reflection;
using System.Reflection.Emit;
using Godot;
using Rpg.Services;
using Rpg.Session.Cache;

namespace ClientTests;

/// <summary>验证 openspec/specs/session-events 的业务订阅只属于缓存与 Godot UI。</summary>
public sealed class EventSubscriptionBoundaryTests
{
    /// <summary>验证 server-map-state 的模块边界：服务端不反向引用客户端服务、世界和 UI。</summary>
    [Fact]
    public void ServerModulesHaveNoClientServiceOrUiDependencies()
    {
        var serverDirectory = Path.Combine(ConfigTestFixture.FindRoot(), "src", "Server", "Modules");
        foreach (var path in Directory.EnumerateFiles(serverDirectory, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(path);
            Assert.DoesNotMatch(@"\bRpg\.(?:Session|World|UI|Services)\b|\bGameServices\s*\.", source);
        }
        var aoi = File.ReadAllText(Path.Combine(serverDirectory, "World", "Cache", "DataCacheWorldRoles.cs"));
        Assert.DoesNotMatch(@"\bSystem\.IO\b|\bWorldStorage\b", aoi);
        var storage = File.ReadAllText(Path.Combine(serverDirectory, "World", "Storage", "WorldStorage.cs"));
        Assert.DoesNotMatch(@"\bWorldAoiTracker\b|\bMockServer\b", storage);
    }

    /// <summary>遍历正式类型的调用指令，防止服务或辅助包装类重新注册协议监听。</summary>
    [Fact]
    public void BusinessSubscriptionsBelongToCachesOrGodotUi()
    {
        var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(OpCode))
            .Select(field => (OpCode)field.GetValue(null)!)
            .ToDictionary(opcode => unchecked((ushort)opcode.Value));
        foreach (var type in typeof(Service).Assembly.GetTypes().Where(type =>
                     type.Namespace?.StartsWith("Rpg.") == true && !type.Namespace.StartsWith("Rpg.Tests")))
        {
            var owner = type;
            while (owner.DeclaringType is not null) owner = owner.DeclaringType;
            var allowed = typeof(CacheBase).IsAssignableFrom(owner) ||
                (owner.Namespace == "Rpg.UI" && typeof(Control).IsAssignableFrom(owner));
            if (allowed) continue;
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static |
                BindingFlags.Instance | BindingFlags.DeclaredOnly).Cast<MethodBase>()
                .Concat(type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance | BindingFlags.Static));
            foreach (var method in methods)
            {
                var bytes = method.GetMethodBody()?.GetILAsByteArray();
                if (bytes is null) continue;
                for (var offset = 0; offset < bytes.Length;)
                {
                    ushort code = bytes[offset++];
                    if (code == 0xfe) code = (ushort)(0xfe00 | bytes[offset++]);
                    var operand = opcodes[code].OperandType;
                    if (operand == OperandType.InlineMethod)
                    {
                        var target = method.Module.ResolveMethod(BitConverter.ToInt32(bytes, offset),
                            type.GetGenericArguments(), method.IsGenericMethod ? method.GetGenericArguments() : null);
                        if (typeof(Service).IsAssignableFrom(owner))
                            Assert.False(target?.DeclaringType == typeof(GameServices),
                                $"{type.FullName}.{method.Name} 必须通过构造绑定服务，不能访问 GameServices");
                        Assert.False(target?.DeclaringType == typeof(EventBusService) &&
                            (target.Name == nameof(EventBusService.Subscribe) || target.Name == "add_Error"),
                            $"{type.FullName}.{method.Name} 不得注册业务事件监听");
                    }
                    offset += operand switch
                    {
                        OperandType.InlineNone => 0,
                        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                        OperandType.InlineVar => 2,
                        OperandType.InlineI8 or OperandType.InlineR => 8,
                        OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(bytes, offset),
                        _ => 4
                    };
                }
            }
        }
    }

    /// <summary>验证 game-services 的统一装配、前置非空依赖与逆序释放，禁止回填。</summary>
    [Fact]
    public void ServicesFollowFixedConstructionAndReverseDisposalOrder()
    {
        Type[] order = [typeof(EnvService), typeof(LogService), typeof(EventBusService), typeof(Rpg.Config.ConfigService),
            typeof(Rpg.Settings.SettingService), typeof(Rpg.Server.Hosting.MockServerService), typeof(Rpg.Net.NetService), typeof(Rpg.Session.SessionService),
            typeof(Rpg.World.WorldService), typeof(Rpg.World.Views.AnimationService), typeof(Rpg.Utils.ItemAssetService)];
        var services = typeof(Service).Assembly.GetTypes().Where(type => !type.IsAbstract && typeof(Service).IsAssignableFrom(type));
        var ownershipOrder = order;
        var nullability = new NullabilityInfoContext();
        Assert.Equal(ownershipOrder.OrderBy(type => type.FullName), services.OrderBy(type => type.FullName));
        foreach (var service in ownershipOrder)
        {
            foreach (var constructor in service.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                foreach (var parameter in constructor.GetParameters().Where(parameter => typeof(IService).IsAssignableFrom(parameter.ParameterType)))
                {
                    Assert.Contains(parameter.ParameterType, ownershipOrder);
                    Assert.Equal(NullabilityState.NotNull, nullability.Create(parameter).ReadState);
                    Assert.False(parameter.HasDefaultValue, $"{service.Name}.{parameter.Name} 不得缺省服务依赖");
                    Assert.True(Array.IndexOf(ownershipOrder, parameter.ParameterType) < Array.IndexOf(ownershipOrder, service),
                        $"{service.Name} 只能依赖构造顺序中的前置服务");
                }
            foreach (var field in service.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                if (typeof(IService).IsAssignableFrom(field.FieldType))
                {
                    Assert.True(field.IsInitOnly, $"{service.Name}.{field.Name} 不能在构造后回填依赖");
                    Assert.Equal(NullabilityState.NotNull, nullability.Create(field).ReadState);
                }
        }

        var source = File.ReadAllText(Path.Combine(ConfigTestFixture.FindRoot(), "src", "Services", "GameServices.cs"));
        var constructions = System.Text.RegularExpressions.Regex.Matches(source, @"new (\w+Service)\(")
            .Select(match => match.Groups[1].Value).ToArray();
        Assert.Equal(order.Select(type => type.Name), constructions);
        var disposal = System.Text.RegularExpressions.Regex.Matches(source, @"foreach \(var service in new IService\[\] \{ ([^}]+) \}\)").Last();
        Assert.True(disposal.Success, "整体释放必须显式列出逆依赖顺序");
        Assert.Equal(new[] { "Items", "Animation", "World", "Session", "Net", "MockServer", "Setting", "Config", "EventBus", "Log", "Env" },
            disposal.Groups[1].Value.Split(',').Select(name => name.Trim()));
    }
}
