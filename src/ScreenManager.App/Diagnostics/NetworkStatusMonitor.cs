using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Serilog;
using Serilog.Core;

namespace OrderBuddy.ScreenManager.App.Diagnostics;

/// <summary>
/// Logs Windows network adapter state/changes and continuously probes local-network endpoints
/// (card terminals, payment proxies) discovered from browser traffic. Writes to the network log.
/// </summary>
public sealed class NetworkStatusMonitor : IDisposable
{
    public const string LogCategory = "OrderBuddy.Network.Status";

    private static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan SummaryInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);
    private const int MaxTrackedEndpoints = 32;

    private readonly ILogger _log;
    private readonly ConcurrentDictionary<string, EndpointState> _endpoints = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();
    private System.Threading.Timer? _probeTimer;
    private System.Threading.Timer? _addressChangeDebounce;
    private DateTimeOffset _lastSummaryUtc = DateTimeOffset.MinValue;
    private int _probing;
    private bool _started;

    public NetworkStatusMonitor(ILogger logger)
    {
        _log = logger.ForContext(Constants.SourceContextPropertyName, LogCategory);
    }

    public void Start()
    {
        lock (_sync)
        {
            if (_started)
            {
                return;
            }

            _started = true;
        }

        LogAdapterSnapshot("startup");
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
        NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
        _probeTimer = new System.Threading.Timer(_ => _ = ProbeAllAsync("interval"), null, ProbeInterval, ProbeInterval);
    }

    /// <summary>Registers a LAN endpoint seen in browser traffic so its reachability is tracked.</summary>
    public void ReportLocalEndpoint(string host, int port, string source)
    {
        if (string.IsNullOrWhiteSpace(host) || port <= 0 || _endpoints.Count >= MaxTrackedEndpoints)
        {
            return;
        }

        var key = $"{host}:{port}";
        var state = new EndpointState(host, port);
        if (!_endpoints.TryAdd(key, state))
        {
            return;
        }

        _log.Information("Local network endpoint discovered. Endpoint: {Endpoint}, Source: {Source}", key, source);
        _ = ProbeAsync(state, "discovered");
    }

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        if (e.IsAvailable)
        {
            _log.Information("Windows network availability changed. IsAvailable: {IsAvailable}", true);
        }
        else
        {
            _log.Warning("Windows network availability changed. IsAvailable: {IsAvailable}", false);
        }
    }

    private void OnNetworkAddressChanged(object? sender, EventArgs e)
    {
        lock (_sync)
        {
            _addressChangeDebounce?.Dispose();
            _addressChangeDebounce = new System.Threading.Timer(_ =>
            {
                LogAdapterSnapshot("address-changed");
                _ = ProbeAllAsync("address-changed");
            }, null, TimeSpan.FromSeconds(2), Timeout.InfiniteTimeSpan);
        }
    }

    private void LogAdapterSnapshot(string reason)
    {
        try
        {
            var available = NetworkInterface.GetIsNetworkAvailable();
            var adapters = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel)
                .ToList();

            _log.Information(
                "Network adapter snapshot. Reason: {Reason}, NetworkAvailable: {Available}, AdapterCount: {Count}",
                reason, available, adapters.Count);

            foreach (var adapter in adapters)
            {
                if (adapter.OperationalStatus != OperationalStatus.Up)
                {
                    _log.Information(
                        "Adapter {Name} ({Type}) status {Status}.",
                        adapter.Name, adapter.NetworkInterfaceType, adapter.OperationalStatus);
                    continue;
                }

                var props = adapter.GetIPProperties();
                var addresses = props.UnicastAddresses
                    .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(a => $"{a.Address}/{a.PrefixLength}");
                var gateways = props.GatewayAddresses.Select(g => g.Address.ToString());
                var dns = props.DnsAddresses
                    .Where(d => d.AddressFamily == AddressFamily.InterNetwork)
                    .Select(d => d.ToString());

                _log.Information(
                    "Adapter {Name} ({Type}, {Description}) Up. SpeedMbps: {Speed}, IPv4: {Addresses}, Gateway: {Gateways}, DNS: {Dns}",
                    adapter.Name,
                    adapter.NetworkInterfaceType,
                    adapter.Description,
                    adapter.Speed / 1_000_000,
                    string.Join(", ", addresses),
                    string.Join(", ", gateways),
                    string.Join(", ", dns));
            }
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Failed to capture network adapter snapshot. Reason: {Reason}", reason);
        }
    }

    private async Task ProbeAllAsync(string reason)
    {
        if (Interlocked.Exchange(ref _probing, 1) == 1)
        {
            return;
        }

        try
        {
            await Task.WhenAll(_endpoints.Values.Select(s => ProbeAsync(s, reason)));

            var now = DateTimeOffset.UtcNow;
            if (_endpoints.Count > 0 && now - _lastSummaryUtc >= SummaryInterval)
            {
                _lastSummaryUtc = now;
                _log.Information(
                    "Local endpoint summary: {Summary}",
                    string.Join("; ", _endpoints.Values.Select(s => s.Describe())));
            }
        }
        finally
        {
            Interlocked.Exchange(ref _probing, 0);
        }
    }

    private async Task ProbeAsync(EndpointState state, string reason)
    {
        var stopwatch = Stopwatch.StartNew();
        bool reachable;
        string detail;

        try
        {
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(ProbeTimeout);
            await client.ConnectAsync(state.Host, state.Port, cts.Token);
            reachable = true;
            detail = "connected";
        }
        catch (OperationCanceledException)
        {
            reachable = false;
            detail = $"timeout after {ProbeTimeout.TotalSeconds:0}s";
        }
        catch (SocketException ex)
        {
            reachable = false;
            detail = ex.SocketErrorCode.ToString();
        }
        catch (Exception ex)
        {
            reachable = false;
            detail = ex.GetType().Name + ": " + ex.Message;
        }

        var elapsedMs = stopwatch.ElapsedMilliseconds;
        var changed = state.Update(reachable, elapsedMs, detail);
        if (!changed)
        {
            return;
        }

        if (reachable)
        {
            _log.Information(
                "Local endpoint REACHABLE. Endpoint: {Endpoint}, TcpConnectMs: {ElapsedMs}, Reason: {Reason}",
                state.Key, elapsedMs, reason);
        }
        else
        {
            _log.Warning(
                "Local endpoint UNREACHABLE. Endpoint: {Endpoint}, Error: {Detail}, ElapsedMs: {ElapsedMs}, Reason: {Reason}",
                state.Key, detail, elapsedMs, reason);
        }
    }

    public void Dispose()
    {
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
        NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
        _probeTimer?.Dispose();
        _addressChangeDebounce?.Dispose();
    }

    private sealed class EndpointState(string host, int port)
    {
        private bool? _reachable;
        private long _lastMs;
        private string _lastDetail = "not probed";

        public string Host { get; } = host;
        public int Port { get; } = port;
        public string Key => $"{Host}:{Port}";

        public bool Update(bool reachable, long elapsedMs, string detail)
        {
            lock (this)
            {
                var changed = _reachable != reachable;
                _reachable = reachable;
                _lastMs = elapsedMs;
                _lastDetail = detail;
                return changed;
            }
        }

        public string Describe()
        {
            lock (this)
            {
                return _reachable switch
                {
                    true => $"{Key} up {_lastMs}ms",
                    false => $"{Key} DOWN ({_lastDetail})",
                    null => $"{Key} not probed"
                };
            }
        }
    }
}
