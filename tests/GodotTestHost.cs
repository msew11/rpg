using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Godot;

namespace Rpg.Tests;

/// <summary>为 client-config-map 和 game-services 验收提供已初始化的 Godot 原生环境。</summary>
public partial class GodotTestHost : Node
{
    /// <summary>独立测试程序集及其依赖所在目录。</summary>
    private string _testDirectory = "";

    /// <summary>加载独立测试程序集，复用 xUnit 运行器并按结果退出。</summary>
    public override async void _Ready()
    {
        try
        {
            var assemblyPath = OS.GetCmdlineUserArgs().Single();
            _testDirectory = Path.GetDirectoryName(assemblyPath)!;
            var context = AssemblyLoadContext.GetLoadContext(typeof(GodotTestHost).Assembly)!;
            context.Resolving += ResolveTestDependency;
            var assembly = context.LoadFromAssemblyPath(assemblyPath);
            var run = assembly.GetType("ClientTests.GodotTestSuite", throwOnError: true)!.GetMethod("Run")!;
            GetTree().Quit(await (Task<int>)run.Invoke(null, null)!);
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
        finally { AssemblyLoadContext.GetLoadContext(typeof(GodotTestHost).Assembly)!.Resolving -= ResolveTestDependency; }
    }

    /// <summary>共享已加载的项目和 Godot 程序集，其余测试依赖仅从测试输出目录解析。</summary>
    private Assembly? ResolveTestDependency(AssemblyLoadContext context, AssemblyName name)
    {
        var loaded = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly => assembly.GetName().Name == name.Name);
        if (loaded is not null) return loaded;
        var path = Path.Combine(_testDirectory, name.Name + ".dll");
        return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
    }
}
