using System;
using System.Diagnostics;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Google.Protobuf;
using Rpg.Config;
using Godot;

namespace Rpg.Services;

/// <summary>统一通信与文件提交日志，只为指定的消息标记着色。</summary>
public sealed class LogService : Service
{
    /// <summary>正式运行绑定环境开关；独立格式测试可不绑定环境并始终开启。</summary>
    private readonly EnvService? _env;
    /// <summary>当前是否允许生成日志内容，网络层据此跳过日志专用解码。</summary>
    public bool Enabled => !IsDisposed && (_env?.ConsoleLogEnabled ?? true);

    /// <summary>富文本输出端，测试可替换为文本收集器。</summary>
    private readonly Action<string> _output;
    /// <summary>IO 使用纯文本输出，避免文件内容被解析为富文本。</summary>
    private readonly Action<string> _plainOutput;
    /// <summary>文件日志压缩为单行，纯文本输出保留中文与字面标记。</summary>
    private static readonly JsonSerializerOptions FileJsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    /// <summary>消息 JSON 字符串禁止富文本标记，数组结构保持原样。</summary>
    private static readonly JsonSerializerOptions MessageJsonOptions = CreateMessageJsonOptions();

    /// <summary>正式运行绑定环境开关，将日志送入 Godot 控制台。</summary>
    public LogService(EnvService env) : this(text => GD.PrintRich(text), text => GD.Print(text), env)
    {
        ArgumentNullException.ThrowIfNull(env);
        ObjectDisposedException.ThrowIf(env.IsDisposed, env);
    }

    /// <summary>隔离验收注入输出端；不绑定环境时常开，便于独立验证格式。</summary>
    internal LogService(Action<string> output, Action<string>? plainOutput = null, EnvService? env = null)
    {
        _env = env;
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _plainOutput = plainOutput ?? output;
    }

    /// <summary>在传输发送前记录请求尝试，只有请求标记为绿色。</summary>
    public void Request(IMessage message) => Message("Request", "green", message);

    /// <summary>按协议 Notice 后缀区分推送与响应，只将对应标记设为橙色。</summary>
    public void Receive(IMessage message)
    {
        ThrowIfDisposed();
        if (!Enabled) return;
        Message(message.Descriptor.Name.EndsWith("Notice", StringComparison.Ordinal)
            ? "Notice" : "Response", "#ffa500", message);
    }

    /// <summary>记录客户端设置成功提交的目标文件及单行 JSON。</summary>
    public void ClientIO(string filename, string content) => FileMessage("Client", filename, content);

    /// <summary>记录模拟服务端成功提交的目标文件及单行 JSON。</summary>
    public void ServerIO(string filename, string content) => FileMessage("Server", filename, content);

    /// <summary>日志服务没有外部资源，释放后拒绝继续输出。</summary>
    public override void Dispose() => IsDisposed = true;

    /// <summary>保留协议名与 protobuf JSON，内容中的富文本符号只作为文字。</summary>
    private void Message(string tag, string color, IMessage message)
    {
        ThrowIfDisposed();
        if (!Enabled) return;
        using var json = JsonDocument.Parse(JsonFormatter.Default.Format(message));
        var content = JsonSerializer.Serialize(json.RootElement, MessageJsonOptions);
        Write($"[Client][color={color}][{tag}][/color]{message.Descriptor.Name}{content}");
    }

    /// <summary>隔离输出端故障，不改变已经发送或保存成功的业务结果。</summary>
    private void Write(string text, bool rich = true)
    {
        ThrowIfDisposed();
        try { (rich ? _output : _plainOutput)(text); }
        catch (Exception error) { Trace.WriteLine(error); }
    }

    /// <summary>检查生命周期后压缩文件内容，通过纯文本端输出。</summary>
    private void FileMessage(string source, string filename, string content)
    {
        ThrowIfDisposed();
        if (!Enabled) return;
        Write($"[{source}][IO][{Path.GetFileName(filename)}]{CompactJson(content)}", false);
    }

    /// <summary>移除 JSON 排版空白，保留字符串值内部的空格和转义换行。</summary>
    private static string CompactJson(string content)
    {
        using var json = JsonDocument.Parse(content);
        return JsonSerializer.Serialize(json.RootElement, FileJsonOptions);
    }

    /// <summary>只转义 JSON 字符串中的方括号，避免终端和编辑器将内容当作 BBCode。</summary>
    private static JsonSerializerOptions CreateMessageJsonOptions()
    {
        var settings = new TextEncoderSettings(UnicodeRanges.All);
        settings.ForbidCharacter('[');
        settings.ForbidCharacter(']');
        return new JsonSerializerOptions { Encoder = JavaScriptEncoder.Create(settings) };
    }
}
