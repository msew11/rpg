using Xunit.Runners;

namespace ClientTests;

/// <summary>在真实 Godot 环境中复用配置及 game-services 的既有 xUnit 验收。</summary>
public static class GodotTestSuite
{
    /// <summary>只执行标记为依赖 Godot 的用例，并将失败和空测试集报告为非零退出码。</summary>
    public static async Task<int> Run()
    {
        using var runner = AssemblyRunner.WithoutAppDomain(typeof(GodotTestSuite).Assembly.Location);
        var completed = new TaskCompletionSource<ExecutionCompleteInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
        var errors = 0;
        runner.TestCaseFilter = test => test.Traits.TryGetValue("Runtime", out var values) && values.Contains("Godot");
        runner.OnTestFailed = failure => Console.Error.WriteLine($"{failure.TestDisplayName}: {failure.ExceptionMessage}\n{failure.ExceptionStackTrace}");
        runner.OnErrorMessage = error =>
        {
            Interlocked.Increment(ref errors);
            Console.Error.WriteLine($"{error.ExceptionMessage}\n{error.ExceptionStackTrace}");
        };
        runner.OnExecutionComplete = result => completed.SetResult(result);
        runner.Start();
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(60));
        while (runner.Status != AssemblyRunnerStatus.Idle) await Task.Delay(10);
        Console.WriteLine($"Godot xUnit: total={result.TotalTests}, failed={result.TestsFailed}, skipped={result.TestsSkipped}");
        return result.TotalTests > 0 && result.TestsFailed == 0 && result.TestsSkipped == 0 && errors == 0 ? 0 : 1;
    }
}
