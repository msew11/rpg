using System.Reflection;
using System.Reflection.Emit;
using Godot;
using Rpg.Services;
using Rpg.Session.Cache;

namespace ClientTests;

/// <summary>验证 openspec/specs/session-events 的业务订阅只属于缓存与 Godot UI。</summary>
public sealed class EventSubscriptionBoundaryTests
{
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
                        Assert.False(target?.DeclaringType == typeof(EventService) &&
                            (target.Name == nameof(EventService.Subscribe) || target.Name == "add_Error"),
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
}
