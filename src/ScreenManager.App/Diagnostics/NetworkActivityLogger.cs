using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using OrderBuddy.ScreenManager.Core.Diagnostics;
using OrderBuddy.ScreenManager.Core.Security;
using Serilog;
using Serilog.Core;

namespace OrderBuddy.ScreenManager.App.Diagnostics;

/// <summary>
/// Records page network traffic, failures (incl. Chromium block reasons), WebSockets, console output
/// and exceptions via the DevTools protocol. Credentials are redacted before logging.
/// </summary>
internal sealed class NetworkActivityLogger : IDisposable
{
    public const string LogCategory = "OrderBuddy.Network.Browser";

    private const int MaxTrackedRequests = 3000;
    private const int MaxBodyLogLength = 2000;

    private static readonly HashSet<string> InterestingTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Document", "XHR", "Fetch", "WebSocket", "EventSource", "Ping", "Preflight", "Other"
    };

    private readonly CoreWebView2 _core;
    private readonly string _screenId;
    private readonly ILogger _log;
    private readonly NetworkStatusMonitor _statusMonitor;
    private readonly Dictionary<string, RequestInfo> _requests = new();
    private readonly Dictionary<string, string> _webSockets = new();
    private readonly List<(CoreWebView2DevToolsProtocolEventReceiver Receiver, EventHandler<CoreWebView2DevToolsProtocolEventReceivedEventArgs> Handler)> _subscriptions = [];
    private bool _disposed;

    private sealed record RequestInfo(string Url, string Method, string Type, bool IsLocal, long StartTimestamp);

    private NetworkActivityLogger(CoreWebView2 core, string screenId, ILogger logger, NetworkStatusMonitor statusMonitor)
    {
        _core = core;
        _screenId = screenId;
        _log = logger.ForContext(Constants.SourceContextPropertyName, LogCategory);
        _statusMonitor = statusMonitor;
    }

    public static ILogger CreateLogger(ILogger logger) =>
        logger.ForContext(Constants.SourceContextPropertyName, LogCategory);

    public static async Task<NetworkActivityLogger?> AttachAsync(
        CoreWebView2 core,
        string screenId,
        ILogger logger,
        NetworkStatusMonitor statusMonitor)
    {
        var instance = new NetworkActivityLogger(core, screenId, logger, statusMonitor);
        try
        {
            instance.Subscribe("Network.requestWillBeSent", instance.OnRequestWillBeSent);
            instance.Subscribe("Network.responseReceived", instance.OnResponseReceived);
            instance.Subscribe("Network.loadingFinished", instance.OnLoadingFinished);
            instance.Subscribe("Network.loadingFailed", instance.OnLoadingFailed);
            instance.Subscribe("Network.webSocketCreated", instance.OnWebSocketCreated);
            instance.Subscribe("Network.webSocketHandshakeResponseReceived", instance.OnWebSocketHandshake);
            instance.Subscribe("Network.webSocketFrameSent", p => instance.OnWebSocketFrame(p, "sent"));
            instance.Subscribe("Network.webSocketFrameReceived", p => instance.OnWebSocketFrame(p, "received"));
            instance.Subscribe("Network.webSocketFrameError", instance.OnWebSocketFrameError);
            instance.Subscribe("Network.webSocketClosed", instance.OnWebSocketClosed);
            instance.Subscribe("Log.entryAdded", instance.OnLogEntryAdded);
            instance.Subscribe("Runtime.consoleAPICalled", instance.OnConsoleApiCalled);
            instance.Subscribe("Runtime.exceptionThrown", instance.OnExceptionThrown);

            await core.CallDevToolsProtocolMethodAsync("Network.enable", "{}");
            await core.CallDevToolsProtocolMethodAsync("Log.enable", "{}");
            await core.CallDevToolsProtocolMethodAsync("Runtime.enable", "{}");

            instance._log.Information("Browser network diagnostics attached. ScreenId: {ScreenId}", screenId);
            return instance;
        }
        catch (Exception ex)
        {
            instance._log.Warning(ex, "Failed to attach browser network diagnostics. ScreenId: {ScreenId}", screenId);
            instance.Dispose();
            return null;
        }
    }

    private void Subscribe(string eventName, Action<JsonElement> handler)
    {
        var receiver = _core.GetDevToolsProtocolEventReceiver(eventName);
        EventHandler<CoreWebView2DevToolsProtocolEventReceivedEventArgs> wrapped = (_, e) =>
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                using var doc = JsonDocument.Parse(e.ParameterObjectAsJson);
                handler(doc.RootElement);
            }
            catch (Exception ex)
            {
                _log.Debug(ex, "Failed to process DevTools event {EventName}.", eventName);
            }
        };

        receiver.DevToolsProtocolEventReceived += wrapped;
        _subscriptions.Add((receiver, wrapped));
    }

    private void OnRequestWillBeSent(JsonElement p)
    {
        var requestId = GetString(p, "requestId");
        if (!p.TryGetProperty("request", out var request) || requestId is null)
        {
            return;
        }

        var url = GetString(request, "url") ?? string.Empty;
        var method = GetString(request, "method") ?? "GET";
        var type = GetString(p, "type") ?? "Other";
        var isLocal = IsLocalUrl(url, out var host, out var port);

        if (p.TryGetProperty("redirectResponse", out var redirect) && _requests.TryGetValue(requestId, out var previous))
        {
            _log.Information(
                "HTTP redirect {Status} {FromUrl} -> {ToUrl}. ScreenId: {ScreenId}",
                GetInt(redirect, "status"),
                SafeUrl(previous.Url),
                SafeUrl(url),
                _screenId);
        }

        if (_requests.Count >= MaxTrackedRequests)
        {
            _requests.Clear();
        }

        _requests[requestId] = new RequestInfo(url, method, type, isLocal, Stopwatch.GetTimestamp());

        if (isLocal)
        {
            _statusMonitor.ReportLocalEndpoint(host!, port, $"browser {type} request");
        }

        if (!isLocal && !InterestingTypes.Contains(type))
        {
            return;
        }

        var body = isLocal ? GetString(request, "postData") : null;
        _log.Information(
            "HTTP -> {Method} {Url}. Type: {Type}, LocalNetwork: {IsLocal}, RequestId: {RequestId}, ScreenId: {ScreenId}{BodySuffix}",
            method,
            SafeUrl(url),
            type,
            isLocal,
            requestId,
            _screenId,
            string.IsNullOrEmpty(body) ? string.Empty : ", Body: " + SensitiveDataRedactor.Redact(body, MaxBodyLogLength));
    }

    private void OnResponseReceived(JsonElement p)
    {
        var requestId = GetString(p, "requestId");
        if (requestId is null || !p.TryGetProperty("response", out var response))
        {
            return;
        }

        var status = GetInt(response, "status");
        var url = GetString(response, "url") ?? string.Empty;
        var remoteIp = GetString(response, "remoteIPAddress");
        var remotePort = GetInt(response, "remotePort");
        var type = GetString(p, "type") ?? "Other";

        _requests.TryGetValue(requestId, out var info);
        var isLocal = info?.IsLocal == true || NetworkAddressClassifier.IsLocalNetworkHost(remoteIp);
        if (isLocal && info is not null && !info.IsLocal)
        {
            _requests[requestId] = info with { IsLocal = true };
        }

        if (isLocal && !string.IsNullOrEmpty(remoteIp) && remotePort > 0)
        {
            _statusMonitor.ReportLocalEndpoint(remoteIp, remotePort, $"browser {type} response");
        }

        var interesting = isLocal || InterestingTypes.Contains(type) || status >= 400;
        if (!interesting)
        {
            return;
        }

        var level = status >= 400 ? Serilog.Events.LogEventLevel.Warning : Serilog.Events.LogEventLevel.Information;
        _log.Write(
            level,
            "HTTP <- {Status} {StatusText} {Url}. Type: {Type}, Remote: {RemoteIp}:{RemotePort}, LocalNetwork: {IsLocal}, FromDiskCache: {FromDiskCache}, FromServiceWorker: {FromServiceWorker}, ElapsedMs: {ElapsedMs}, RequestId: {RequestId}, ScreenId: {ScreenId}",
            status,
            GetString(response, "statusText"),
            SafeUrl(url),
            type,
            remoteIp,
            remotePort,
            isLocal,
            GetBool(response, "fromDiskCache"),
            GetBool(response, "fromServiceWorker"),
            ElapsedMs(info),
            requestId,
            _screenId);
    }

    private async void OnLoadingFinished(JsonElement p)
    {
        var requestId = GetString(p, "requestId");
        if (requestId is null || !_requests.Remove(requestId, out var info) || !info.IsLocal)
        {
            return;
        }

        try
        {
            var raw = await _core.CallDevToolsProtocolMethodAsync(
                "Network.getResponseBody",
                JsonSerializer.Serialize(new { requestId }));

            using var doc = JsonDocument.Parse(raw);
            var body = GetString(doc.RootElement, "body") ?? string.Empty;
            if (GetBool(doc.RootElement, "base64Encoded"))
            {
                body = $"<binary {body.Length} base64 chars>";
            }

            _log.Information(
                "Local network response body. {Method} {Url}, ElapsedMs: {ElapsedMs}, RequestId: {RequestId}, ScreenId: {ScreenId}, Body: {Body}",
                info.Method,
                SafeUrl(info.Url),
                ElapsedMs(info),
                requestId,
                _screenId,
                SensitiveDataRedactor.Redact(body, MaxBodyLogLength));
        }
        catch (Exception ex)
        {
            _log.Debug(ex, "Could not read local network response body. RequestId: {RequestId}", requestId);
        }
    }

    private void OnLoadingFailed(JsonElement p)
    {
        var requestId = GetString(p, "requestId");
        if (requestId is null)
        {
            return;
        }

        _requests.Remove(requestId, out var info);
        var canceled = GetBool(p, "canceled");
        var blockedReason = GetString(p, "blockedReason");
        string? corsError = null;
        string? corsParameter = null;
        if (p.TryGetProperty("corsErrorStatus", out var cors))
        {
            corsError = GetString(cors, "corsError");
            corsParameter = GetString(cors, "failedParameter");
        }

        var isLocal = info?.IsLocal == true;
        var level = canceled && !isLocal && blockedReason is null && corsError is null
            ? Serilog.Events.LogEventLevel.Information
            : Serilog.Events.LogEventLevel.Warning;

        _log.Write(
            level,
            "HTTP FAILED {Method} {Url}. Error: {ErrorText}, BlockedReason: {BlockedReason}, CorsError: {CorsError}, CorsFailedParameter: {CorsParameter}, Canceled: {Canceled}, Type: {Type}, LocalNetwork: {IsLocal}, ElapsedMs: {ElapsedMs}, RequestId: {RequestId}, ScreenId: {ScreenId}",
            info?.Method,
            SafeUrl(info?.Url ?? "(unknown)"),
            GetString(p, "errorText"),
            blockedReason,
            corsError,
            corsParameter,
            canceled,
            GetString(p, "type"),
            isLocal,
            ElapsedMs(info),
            requestId,
            _screenId);
    }

    private void OnWebSocketCreated(JsonElement p)
    {
        var requestId = GetString(p, "requestId");
        var url = GetString(p, "url") ?? string.Empty;
        if (requestId is not null)
        {
            _webSockets[requestId] = url;
        }

        var isLocal = IsLocalUrl(url, out var host, out var port);
        if (isLocal)
        {
            _statusMonitor.ReportLocalEndpoint(host!, port, "browser WebSocket");
        }

        _log.Information(
            "WebSocket created {Url}. LocalNetwork: {IsLocal}, RequestId: {RequestId}, ScreenId: {ScreenId}",
            SafeUrl(url), isLocal, requestId, _screenId);
    }

    private void OnWebSocketHandshake(JsonElement p)
    {
        var requestId = GetString(p, "requestId");
        var status = p.TryGetProperty("response", out var response) ? GetInt(response, "status") : 0;
        _log.Information(
            "WebSocket handshake {Status} {Url}. RequestId: {RequestId}, ScreenId: {ScreenId}",
            status, SafeUrl(WebSocketUrl(requestId)), requestId, _screenId);
    }

    private void OnWebSocketFrame(JsonElement p, string direction)
    {
        var requestId = GetString(p, "requestId");
        var url = WebSocketUrl(requestId);
        var payload = p.TryGetProperty("response", out var response) ? GetString(response, "payloadData") : null;
        var isLocal = IsLocalUrl(url, out _, out _);

        _log.Write(
            isLocal ? Serilog.Events.LogEventLevel.Information : Serilog.Events.LogEventLevel.Debug,
            "WebSocket frame {Direction} {Url}. Payload: {Payload}, RequestId: {RequestId}, ScreenId: {ScreenId}",
            direction,
            SafeUrl(url),
            SensitiveDataRedactor.Redact(payload, 1000),
            requestId,
            _screenId);
    }

    private void OnWebSocketFrameError(JsonElement p)
    {
        var requestId = GetString(p, "requestId");
        _log.Warning(
            "WebSocket frame error {Url}. Error: {Error}, RequestId: {RequestId}, ScreenId: {ScreenId}",
            SafeUrl(WebSocketUrl(requestId)), GetString(p, "errorMessage"), requestId, _screenId);
    }

    private void OnWebSocketClosed(JsonElement p)
    {
        var requestId = GetString(p, "requestId");
        var url = WebSocketUrl(requestId);
        if (requestId is not null)
        {
            _webSockets.Remove(requestId);
        }

        _log.Information(
            "WebSocket closed {Url}. RequestId: {RequestId}, ScreenId: {ScreenId}",
            SafeUrl(url), requestId, _screenId);
    }

    private void OnLogEntryAdded(JsonElement p)
    {
        if (!p.TryGetProperty("entry", out var entry))
        {
            return;
        }

        var level = GetString(entry, "level");
        _log.Write(
            level is "error" or "warning" ? Serilog.Events.LogEventLevel.Warning : Serilog.Events.LogEventLevel.Information,
            "Browser log [{Source}/{Level}] {Text}. Url: {Url}, RequestId: {RequestId}, ScreenId: {ScreenId}",
            GetString(entry, "source"),
            level,
            SensitiveDataRedactor.Redact(GetString(entry, "text"), MaxBodyLogLength),
            SafeUrl(GetString(entry, "url") ?? string.Empty),
            GetString(entry, "networkRequestId"),
            _screenId);
    }

    private void OnConsoleApiCalled(JsonElement p)
    {
        var type = GetString(p, "type") ?? "log";
        var text = new StringBuilder();
        if (p.TryGetProperty("args", out var args) && args.ValueKind == JsonValueKind.Array)
        {
            foreach (var arg in args.EnumerateArray())
            {
                if (text.Length > 0)
                {
                    text.Append(' ');
                }

                text.Append(DescribeRemoteObject(arg));
            }
        }

        _log.Write(
            type is "error" or "assert" or "warning" ? Serilog.Events.LogEventLevel.Warning : Serilog.Events.LogEventLevel.Information,
            "Page console.{ConsoleType}: {Text}. ScreenId: {ScreenId}",
            type,
            SensitiveDataRedactor.Redact(text.ToString(), MaxBodyLogLength),
            _screenId);
    }

    private void OnExceptionThrown(JsonElement p)
    {
        if (!p.TryGetProperty("exceptionDetails", out var details))
        {
            return;
        }

        var description = details.TryGetProperty("exception", out var exception)
            ? GetString(exception, "description")
            : null;

        _log.Warning(
            "Page exception: {Text} {Description}. Url: {Url}, Line: {Line}, ScreenId: {ScreenId}",
            GetString(details, "text"),
            SensitiveDataRedactor.Redact(description, MaxBodyLogLength),
            SafeUrl(GetString(details, "url") ?? string.Empty),
            GetInt(details, "lineNumber"),
            _screenId);
    }

    private static string DescribeRemoteObject(JsonElement arg)
    {
        if (arg.TryGetProperty("value", out var value))
        {
            return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText();
        }

        if (arg.TryGetProperty("preview", out var preview) && preview.TryGetProperty("properties", out var props))
        {
            var parts = props.EnumerateArray()
                .Select(prop => $"\"{GetString(prop, "name")}\":\"{GetString(prop, "value")}\"");
            return "{" + string.Join(",", parts) + "}";
        }

        return GetString(arg, "description") ?? GetString(arg, "type") ?? string.Empty;
    }

    private string WebSocketUrl(string? requestId) =>
        requestId is not null && _webSockets.TryGetValue(requestId, out var url) ? url : "(unknown)";

    private static bool IsLocalUrl(string url, out string? host, out int port)
    {
        host = null;
        port = 0;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.IsFile || string.IsNullOrEmpty(uri.Host))
        {
            return false;
        }

        host = uri.Host;
        port = uri.Port;
        return NetworkAddressClassifier.IsLocalNetworkHost(uri.Host);
    }

    private static string SafeUrl(string url)
    {
        if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return SensitiveDataRedactor.Truncate(url, 60);
        }

        return SensitiveDataRedactor.Redact(url, 500);
    }

    private static long ElapsedMs(RequestInfo? info) =>
        info is null ? -1 : (long)Stopwatch.GetElapsedTime(info.StartTimestamp).TotalMilliseconds;

    private static string? GetString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var v)
            ? v.ValueKind == JsonValueKind.String ? v.GetString() : v.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : v.GetRawText()
            : null;

    private static int GetInt(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d)
            ? (int)d
            : 0;

    private static bool GetBool(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var (receiver, handler) in _subscriptions)
        {
            try
            {
                receiver.DevToolsProtocolEventReceived -= handler;
            }
            catch (Exception)
            {
                // Browser process may already be gone; nothing to unhook.
            }
        }

        _subscriptions.Clear();
        _requests.Clear();
        _webSockets.Clear();
    }
}
