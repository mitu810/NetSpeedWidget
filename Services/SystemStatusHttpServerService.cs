using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using NetSpeedWidget.Models;

namespace NetSpeedWidget.Services
{
    public sealed class SystemStatusHttpServerService : IDisposable
    {
        private const int MinPort = 1;
        private const int MaxPort = 65535;
        private const string JsonContentType = "application/json; charset=utf-8";
        private const string HtmlContentType = "text/html; charset=utf-8";
        private const string AuthorizationHeaderName = "Authorization";
        private const string BearerPrefix = "Bearer ";
        private static readonly TimeSpan AuthTokenLifetime = TimeSpan.FromHours(12);
        private static readonly string[] KnownCardKeys =
        {
            "network",
            "cpuUsage",
            "cpuTemperature",
            "gpuUsage",
            "gpuTemperature",
            "memory"
        };

        private static readonly JsonSerializerOptions StatusJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly object _syncRoot = new();
        private readonly SystemStatusCollectorService _collectorService;
        private readonly SettingsService _settingsService;
        private TcpListener? _listener;
        private CancellationTokenSource? _acceptCancellation;
        private Task? _acceptTask;
        private readonly SemaphoreSlim _clientSlots = new(16, 16);
        private AppSettings _appSettings = new();
        private SystemStatusSettings _settings = new();
        private AppThemeMode _appTheme = AppThemeMode.Dark;
        private int _requestedPort;
        private bool _lastPasswordEnabled;
        private string _lastPasswordHash = string.Empty;
        private string _authToken = string.Empty;
        private DateTimeOffset _authTokenExpiresAt = DateTimeOffset.MinValue;

        public SystemStatusHttpServerService()
            : this(new SystemStatusCollectorService(), new SettingsService())
        {
        }

        public SystemStatusHttpServerService(SystemStatusCollectorService collectorService)
            : this(collectorService, new SettingsService())
        {
        }

        public SystemStatusHttpServerService(
            SystemStatusCollectorService collectorService,
            SettingsService settingsService)
        {
            _collectorService = collectorService ?? throw new ArgumentNullException(nameof(collectorService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            LastError = string.Empty;
        }

        public bool IsRunning { get; private set; }

        public int ActualPort { get; private set; }

        public string LastError { get; private set; }

        /// <summary>
        /// Returns the local browser entry URL after the TCP listener has started.
        /// </summary>
        public string GetLocalAccessUrl()
        {
            lock (_syncRoot)
            {
                return IsRunning && ActualPort > 0
                    ? CreateAccessUrl("localhost", ActualPort)
                    : string.Empty;
            }
        }

        /// <summary>
        /// Returns local and LAN browser entry URLs after the TCP listener has started.
        /// </summary>
        public IReadOnlyList<string> GetAccessUrls()
        {
            bool isRunning;
            int actualPort;

            lock (_syncRoot)
            {
                isRunning = IsRunning;
                actualPort = ActualPort;
            }

            return GetAccessUrls(isRunning, actualPort);
        }

        /// <summary>
        /// Returns a small runtime snapshot for SettingsWindow.
        /// </summary>
        public SystemStatusHttpServerState GetState()
        {
            bool isRunning;
            int actualPort;
            string lastError;

            lock (_syncRoot)
            {
                isRunning = IsRunning;
                actualPort = ActualPort;
                lastError = LastError;
            }

            return new SystemStatusHttpServerState(
                isRunning,
                actualPort,
                GetAccessUrls(isRunning, actualPort),
                lastError);
        }

        /// <summary>
        /// Stops any old listener, then starts from the requested port and nearby fallback ports.
        /// </summary>
        public void Start(int preferredPort)
        {
            var normalizedPreferredPort = NormalizePort(preferredPort);

            lock (_syncRoot)
            {
                if (IsRunning && _requestedPort == normalizedPreferredPort)
                {
                    return;
                }
            }

            Stop();

            Exception? lastException = null;

            foreach (var candidatePort in GetCandidatePorts(normalizedPreferredPort))
            {
                try
                {
                    var listener = CreatePreferredListener(candidatePort);
                    listener.Start();

                    var cancellation = new CancellationTokenSource();
                    var token = cancellation.Token;
                    var acceptTask = Task.Run(
                        () => AcceptLoopAsync(listener, token),
                        token);

                    lock (_syncRoot)
                    {
                        _listener = listener;
                        _acceptCancellation = cancellation;
                        _acceptTask = acceptTask;
                        _requestedPort = normalizedPreferredPort;
                        ActualPort = ((IPEndPoint)listener.LocalEndpoint).Port;
                        IsRunning = true;
                        LastError = string.Empty;
                    }

                    return;
                }
                catch (Exception ex) when (ex is SocketException or InvalidOperationException or ObjectDisposedException)
                {
                    lastException = ex;
                }
            }

            lock (_syncRoot)
            {
                IsRunning = false;
                ActualPort = 0;
                _requestedPort = 0;
                LastError = lastException?.Message ?? "Unable to start system status HTTP service.";
            }
        }

        /// <summary>
        /// Stops the TCP listener and clears runtime state. It is safe to call repeatedly.
        /// </summary>
        public void Stop()
        {
            TcpListener? listener;
            CancellationTokenSource? cancellation;

            lock (_syncRoot)
            {
                listener = _listener;
                cancellation = _acceptCancellation;
                _listener = null;
                _acceptCancellation = null;
                _acceptTask = null;
                _requestedPort = 0;
                ActualPort = 0;
                IsRunning = false;
            }

            try
            {
                cancellation?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            try
            {
                listener?.Stop();
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }

            cancellation?.Dispose();
        }

        /// <summary>
        /// Starts the service only when both system status and its HTTP server are enabled.
        /// </summary>
        public void ApplySettings(AppSettings settings)
        {
            if (settings is null)
            {
                Stop();
                return;
            }

            StoreSettings(settings);
            ApplySystemStatusSettings(settings.SystemStatus);
        }

        /// <summary>
        /// Starts the service only when both system status and its HTTP server are enabled.
        /// </summary>
        public void ApplySettings(SystemStatusSettings settings)
        {
            StoreSettings(settings);
            ApplySystemStatusSettings(settings);
        }

        public void Dispose()
        {
            Stop();
            _collectorService.Dispose();
        }

        public static bool ShouldRun(SystemStatusSettings settings)
        {
            return settings is not null && settings.Enabled && settings.ServerEnabled;
        }

        public static IReadOnlyList<int> GetCandidatePorts(int preferredPort, int maxAttempts = 20)
        {
            if (maxAttempts <= 0)
            {
                return Array.Empty<int>();
            }

            var attemptCount = Math.Min(maxAttempts, MaxPort);
            var ports = new List<int>(attemptCount);
            var currentPort = NormalizePort(preferredPort);

            for (var index = 0; index < attemptCount; index++)
            {
                ports.Add(currentPort);
                currentPort++;

                if (currentPort > MaxPort)
                {
                    currentPort = MinPort;
                }
            }

            return ports;
        }

        public static string CreateAccessUrl(string host, int port)
        {
            var normalizedHost =
                string.IsNullOrWhiteSpace(host)
                    ? "localhost"
                    : host.Trim();

            if (IPAddress.TryParse(normalizedHost, out var parsedAddress) &&
                parsedAddress.AddressFamily == AddressFamily.InterNetworkV6 &&
                !normalizedHost.StartsWith("[", StringComparison.Ordinal))
            {
                normalizedHost = $"[{normalizedHost}]";
            }

            return $"http://{normalizedHost}:{NormalizePort(port)}/";
        }

        public static TcpListener CreatePreferredListener(int port)
        {
            if (Socket.OSSupportsIPv6)
            {
                try
                {
                    var listener = new TcpListener(IPAddress.IPv6Any, NormalizePort(port));
                    listener.Server.DualMode = true;
                    return listener;
                }
                catch (Exception ex) when (ex is SocketException or NotSupportedException)
                {
                }
            }

            return new TcpListener(IPAddress.Any, NormalizePort(port));
        }

        public static IReadOnlyList<string> GetLanIPAddresses()
        {
            var addresses = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            try
            {
                foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                        networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    {
                        continue;
                    }

                    foreach (var unicastAddress in networkInterface.GetIPProperties().UnicastAddresses)
                    {
                        var address = unicastAddress.Address;

                        if (!IsUsableLanAddress(address))
                        {
                            continue;
                        }

                        var addressText = address.ToString();

                        if (addressText.StartsWith("169.254.", StringComparison.Ordinal) ||
                            !seen.Add(addressText))
                        {
                            continue;
                        }

                        addresses.Add(addressText);
                    }
                }
            }
            catch (Exception ex) when (ex is NetworkInformationException or SocketException or InvalidOperationException)
            {
                return Array.Empty<string>();
            }

            return addresses;
        }

        public static IReadOnlyList<string> GetLanIPv4Addresses()
        {
            return GetLanIPAddresses()
                .Where(addressText =>
                    IPAddress.TryParse(addressText, out var address) &&
                    address.AddressFamily == AddressFamily.InterNetwork)
                .ToArray();
        }

        private static bool IsUsableLanAddress(IPAddress address)
        {
            if (IPAddress.IsLoopback(address))
            {
                return false;
            }

            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                return !address.ToString().StartsWith("169.254.", StringComparison.Ordinal);
            }

            if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                return !address.IsIPv6LinkLocal &&
                    !address.IsIPv6SiteLocal &&
                    !address.IsIPv6Multicast;
            }

            return false;
        }

        public static string CreateStatusJson(SystemStatusSnapshot snapshot)
        {
            return JsonSerializer.Serialize(snapshot, StatusJsonOptions);
        }

        /// <summary>生成带盐的密码派生值，保持原有接口入口。</summary>
        public static string CreatePasswordHash(string password) => PasswordHashService.Create(password);

        /// <summary>验证访问密码，兼容旧版本的 SHA-256 配置。</summary>
        public static bool VerifyPassword(string password, string passwordHash) => PasswordHashService.Verify(password, passwordHash);

        public static bool TryParseLoginJson(string requestBody, out string password)
        {
            password = string.Empty;

            if (string.IsNullOrWhiteSpace(requestBody))
            {
                return false;
            }

            try
            {
                var request =
                    JsonSerializer.Deserialize<LoginRequest>(
                        requestBody,
                        StatusJsonOptions);

                if (string.IsNullOrWhiteSpace(request?.Password))
                {
                    return false;
                }

                password = request.Password;
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        public static string CreateAuthStateJson(SystemStatusSettings settings)
        {
            return JsonSerializer.Serialize(
                new
                {
                    PasswordEnabled = IsPasswordAuthenticationConfigured(settings)
                },
                StatusJsonOptions);
        }

        public static string CreateLoginResponseJson(
            string token,
            DateTimeOffset? expiresAt,
            bool passwordEnabled)
        {
            return JsonSerializer.Serialize(
                new
                {
                    PasswordEnabled = passwordEnabled,
                    Token = token ?? string.Empty,
                    ExpiresAt = expiresAt
                },
                StatusJsonOptions);
        }

        public static string CreateUnauthorizedResponse()
        {
            return CreateHttpResponse(
                401,
                "Unauthorized",
                JsonContentType,
                "{\"error\":\"unauthorized\"}");
        }

        public static string CreateSettingsJson(
            SystemStatusSettings settings,
            AppThemeMode appTheme = AppThemeMode.Dark)
        {
            settings ??= new SystemStatusSettings();

            var effectiveTheme = settings.WebTheme switch
            {
                SystemStatusWebTheme.Light => AppThemeMode.Light,
                SystemStatusWebTheme.Dark => AppThemeMode.Dark,
                _ => appTheme
            };

            return JsonSerializer.Serialize(
                new
                {
                    settings.RefreshIntervalSeconds,
                    settings.WebTheme,
                    EffectiveTheme = effectiveTheme,
                    settings.Cards,
                    settings.Layout
                },
                StatusJsonOptions);
        }

        public static IReadOnlyList<SystemStatusCardLayoutItem> NormalizeLayout(
            IEnumerable<SystemStatusCardLayoutItem>? layout)
        {
            var result = new List<SystemStatusCardLayoutItem>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            if (layout is not null)
            {
                foreach (var item in layout)
                {
                    if (item is null ||
                        string.IsNullOrWhiteSpace(item.CardKey) ||
                        Array.IndexOf(KnownCardKeys, item.CardKey) < 0 ||
                        !seen.Add(item.CardKey))
                    {
                        continue;
                    }

                    result.Add(
                        new SystemStatusCardLayoutItem
                        {
                            CardKey = item.CardKey,
                            Order = result.Count
                        });
                }
            }

            foreach (var cardKey in KnownCardKeys)
            {
                if (seen.Add(cardKey))
                {
                    result.Add(
                        new SystemStatusCardLayoutItem
                        {
                            CardKey = cardKey,
                            Order = result.Count
                        });
                }
            }

            return result;
        }

        public static bool TryParseLayoutJson(
            string requestBody,
            out IReadOnlyList<SystemStatusCardLayoutItem> layout)
        {
            layout = Array.Empty<SystemStatusCardLayoutItem>();

            if (string.IsNullOrWhiteSpace(requestBody))
            {
                return false;
            }

            try
            {
                var request =
                    JsonSerializer.Deserialize<LayoutUpdateRequest>(
                        requestBody,
                        StatusJsonOptions);

                if (request?.Layout is null ||
                    request.Layout.Count == 0)
                {
                    return false;
                }

                layout = NormalizeLayout(request.Layout);
                return layout.Count > 0;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        public static bool TryParseThemeJson(
            string requestBody,
            out SystemStatusWebTheme webTheme)
        {
            webTheme = SystemStatusWebTheme.FollowApp;

            if (string.IsNullOrWhiteSpace(requestBody))
            {
                return false;
            }

            try
            {
                var request =
                    JsonSerializer.Deserialize<ThemeUpdateRequest>(
                        requestBody,
                        StatusJsonOptions);

                if (string.IsNullOrWhiteSpace(request?.WebTheme) ||
                    !Enum.TryParse<SystemStatusWebTheme>(
                        request.WebTheme,
                        ignoreCase: true,
                        out var parsedTheme))
                {
                    return false;
                }

                webTheme = parsedTheme;
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        public static bool RequiresAuthentication(
            SystemStatusSettings settings,
            string method,
            string path)
        {
            return settings?.PasswordEnabled == true &&
                !string.IsNullOrWhiteSpace(settings.PasswordHash) &&
                IsProtectedPath(method, path);
        }

        public static bool IsPasswordAuthenticationConfigured(SystemStatusSettings? settings)
        {
            return settings?.PasswordEnabled == true &&
                !string.IsNullOrWhiteSpace(settings.PasswordHash);
        }

        public static bool IsProtectedPath(string method, string path)
        {
            if (string.IsNullOrWhiteSpace(method) ||
                string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(path, "/api/status", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(path, "/api/settings", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(path, "/api/layout", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(path, "/api/theme", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return false;
        }

        public static string? GetBearerToken(IReadOnlyDictionary<string, string> headers)
        {
            if (headers is null ||
                !headers.TryGetValue(AuthorizationHeaderName, out var authorization) ||
                string.IsNullOrWhiteSpace(authorization) ||
                !authorization.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var token = authorization[BearerPrefix.Length..].Trim();
            return string.IsNullOrWhiteSpace(token) ? null : token;
        }

        public static string CreateDashboardHtml()
        {
            using var stream = typeof(SystemStatusHttpServerService).Assembly.GetManifestResourceStream("NetSpeedWidget.Assets.Dashboard.html")
                ?? throw new InvalidOperationException("缺少仪表盘页面资源。");
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }

        public static string GetRequestPath(string requestLine)
        {
            if (string.IsNullOrWhiteSpace(requestLine))
            {
                return "/";
            }

            var parts = requestLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                return "/";
            }

            var target = parts[1];
            var path = Uri.TryCreate(target, UriKind.Absolute, out var absoluteUri)
                ? absoluteUri.AbsolutePath
                : target;

            var queryIndex = path.IndexOf('?', StringComparison.Ordinal);
            if (queryIndex >= 0)
            {
                path = path[..queryIndex];
            }

            return string.IsNullOrWhiteSpace(path) ? "/" : path;
        }

        public static string CreateHttpResponse(
            int statusCode,
            string reasonPhrase,
            string contentType,
            string body)
        {
            var safeBody = body ?? string.Empty;
            var contentLength = Encoding.UTF8.GetByteCount(safeBody);
            var builder = new StringBuilder();

            builder.Append("HTTP/1.1 ")
                .Append(statusCode)
                .Append(' ')
                .Append(reasonPhrase)
                .Append("\r\n");
            builder.Append("Access-Control-Allow-Origin: *\r\n");
            builder.Append("Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n");
            builder.Append("Access-Control-Allow-Headers: Content-Type, Authorization\r\n");
            builder.Append("Connection: close\r\n");
            builder.Append("Content-Length: ")
                .Append(contentLength)
                .Append("\r\n");

            if (!string.IsNullOrWhiteSpace(contentType))
            {
                builder.Append("Content-Type: ")
                    .Append(contentType)
                    .Append("\r\n");
            }

            builder.Append("\r\n");
            builder.Append(safeBody);

            return builder.ToString();
        }

        private static IReadOnlyList<string> GetAccessUrls(bool isRunning, int actualPort)
        {
            if (!isRunning || actualPort <= 0)
            {
                return Array.Empty<string>();
            }

            var urls = new List<string>
            {
                CreateAccessUrl("localhost", actualPort)
            };

            foreach (var lanAddress in GetLanIPAddresses())
            {
                urls.Add(CreateAccessUrl(lanAddress, actualPort));
            }

            return urls;
        }

        private void ApplySystemStatusSettings(SystemStatusSettings settings)
        {
            if (ShouldRun(settings))
            {
                Start(settings.PreferredPort);
                return;
            }

            Stop();
        }

        private void StoreSettings(AppSettings settings)
        {
            lock (_syncRoot)
            {
                _appSettings = SettingsService.Clone(settings ?? new AppSettings());
                _settings = _appSettings.SystemStatus ?? new SystemStatusSettings();
                _appTheme = _appSettings.Theme;
                RefreshAuthTokenState();
            }
        }

        private void StoreSettings(SystemStatusSettings settings)
        {
            lock (_syncRoot)
            {
                _appSettings.SystemStatus = settings ?? new SystemStatusSettings();
                _settings = _appSettings.SystemStatus;
                RefreshAuthTokenState();
            }
        }

        private (SystemStatusSettings Settings, AppThemeMode AppTheme) GetRuntimeSettings()
        {
            lock (_syncRoot)
            {
                return (SettingsService.Clone(_appSettings).SystemStatus, _appTheme);
            }
        }

        private void RefreshAuthTokenState()
        {
            if (_lastPasswordEnabled == _settings.PasswordEnabled &&
                string.Equals(_lastPasswordHash, _settings.PasswordHash, StringComparison.Ordinal))
            {
                return;
            }

            _lastPasswordEnabled = _settings.PasswordEnabled;
            _lastPasswordHash = _settings.PasswordHash ?? string.Empty;
            _authToken = string.Empty;
            _authTokenExpiresAt = DateTimeOffset.MinValue;
        }

        private async Task AcceptLoopAsync(TcpListener listener, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var client = await listener.AcceptTcpClientAsync(cancellationToken);
                    if (!_clientSlots.Wait(0))
                    {
                        client.Dispose();
                        continue;
                    }
                    _ = HandleAdmittedClientAsync(client, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex) when (ex is SocketException or InvalidOperationException or IOException)
                {
                    SetLastError(ex.Message);
                    await DelayAfterAcceptErrorAsync(cancellationToken);
                }
            }
        }

        /// <summary>确保连接结束后释放并发名额。</summary>
        private async Task HandleAdmittedClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            try { await HandleClientAsync(client, cancellationToken); }
            finally { _clientSlots.Release(); }
        }

        /// <summary>有界读取请求；超时或服务停止时释放连接。</summary>
        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            using (client)
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                deadline.CancelAfter(TimeSpan.FromSeconds(10));
                var stream = client.GetStream();
                try
                {
                    var request = await HttpRequestReader.ReadAsync(stream, deadline.Token);
                    var response = await CreateResponseAsync(request.RequestLine, request.Headers, request.Body);
                    await stream.WriteAsync(Encoding.UTF8.GetBytes(response), deadline.Token);
                    await stream.FlushAsync(deadline.Token);
                }
                catch (LocalHttpRequestException ex)
                {
                    var response = CreateHttpResponse(ex.StatusCode,
                        ex.StatusCode == 413 ? "Payload Too Large" : ex.StatusCode == 431 ? "Request Header Fields Too Large" : "Bad Request",
                        JsonContentType, "{\"error\":\"invalidRequest\"}");
                    try { await stream.WriteAsync(Encoding.UTF8.GetBytes(response), deadline.Token); }
                    catch (Exception writeError) when (writeError is IOException or OperationCanceledException) { }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException or InvalidOperationException)
                {
                    SetLastError(ex.Message);
                }
            }
        }

        private async Task<string> CreateResponseAsync(
            string requestLine,
            IReadOnlyDictionary<string, string> headers,
            string requestBody)
        {
            var method = GetRequestMethod(requestLine);
            if (string.Equals(method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                return CreateHttpResponse(
                    204,
                    "No Content",
                    string.Empty,
                    string.Empty);
            }

            var path = GetRequestPath(requestLine);
            var runtimeSettings = GetRuntimeSettings();

            if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(path, "/api/auth/state", StringComparison.OrdinalIgnoreCase))
            {
                return CreateHttpResponse(
                    200,
                    "OK",
                    JsonContentType,
                    CreateAuthStateJson(runtimeSettings.Settings));
            }

            if (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(path, "/api/auth/login", StringComparison.OrdinalIgnoreCase))
            {
                return Login(requestBody);
            }

            if (RequiresAuthentication(runtimeSettings.Settings, method, path) &&
                !IsAuthorized(headers))
            {
                return CreateUnauthorizedResponse();
            }

            if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(path, "/api/status", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var snapshot = await _collectorService.CollectAsync();
                    return CreateHttpResponse(
                        200,
                        "OK",
                        JsonContentType,
                        CreateStatusJson(snapshot));
                }
                catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
                {
                    SetLastError(ex.Message);
                    return CreateHttpResponse(
                        500,
                        "Internal Server Error",
                        JsonContentType,
                        "{\"error\":\"internalServerError\"}");
                }
            }

            if (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(path, "/api/layout", StringComparison.OrdinalIgnoreCase))
            {
                return SaveLayout(requestBody);
            }

            if (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(path, "/api/theme", StringComparison.OrdinalIgnoreCase))
            {
                return SaveTheme(requestBody);
            }

            if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(path, "/api/settings", StringComparison.OrdinalIgnoreCase))
            {
                return CreateHttpResponse(
                    200,
                    "OK",
                    JsonContentType,
                    CreateSettingsJson(runtimeSettings.Settings, runtimeSettings.AppTheme));
            }

            if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(path, "/", StringComparison.Ordinal))
            {
                return CreateHttpResponse(
                    200,
                    "OK",
                    HtmlContentType,
                    CreateDashboardHtml());
            }

            return CreateHttpResponse(
                404,
                "Not Found",
                JsonContentType,
                "{\"error\":\"notFound\"}");
        }

        private string Login(string requestBody)
        {
            var runtimeSettings = GetRuntimeSettings().Settings;

            if (!IsPasswordAuthenticationConfigured(runtimeSettings))
            {
                return CreateHttpResponse(
                    200,
                    "OK",
                    JsonContentType,
                    CreateLoginResponseJson(string.Empty, null, false));
            }

            if (!TryParseLoginJson(requestBody, out var password) ||
                !VerifyPassword(password, runtimeSettings.PasswordHash))
            {
                return CreateUnauthorizedResponse();
            }

            var token = GenerateAuthToken();
            var expiresAt = DateTimeOffset.Now.Add(AuthTokenLifetime);

            lock (_syncRoot)
            {
                _authToken = token;
                _authTokenExpiresAt = expiresAt;
            }

            return CreateHttpResponse(
                200,
                "OK",
                JsonContentType,
                CreateLoginResponseJson(token, expiresAt, true));
        }

        private bool IsAuthorized(IReadOnlyDictionary<string, string> headers)
        {
            var token = GetBearerToken(headers);

            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            lock (_syncRoot)
            {
                return !string.IsNullOrWhiteSpace(_authToken) &&
                    DateTimeOffset.Now < _authTokenExpiresAt &&
                    FixedTimeEquals(token, _authToken);
            }
        }

        private string SaveTheme(string requestBody)
        {
            if (!TryParseThemeJson(requestBody, out var webTheme))
            {
                return CreateHttpResponse(
                    400,
                    "Bad Request",
                    JsonContentType,
                    "{\"error\":\"invalidTheme\"}");
            }

            AppThemeMode appTheme;
            AppSettings before;
            lock (_syncRoot)
            {
                before = SettingsService.Clone(_appSettings);
                _appSettings.SystemStatus.WebTheme = webTheme;
                _settings = _appSettings.SystemStatus;
                appTheme = _appTheme;
            }

            try
            {
                lock (_syncRoot)
                {
                    _appSettings = _settingsService.MergeAndSave(_appSettings, before);
                    _settings = _appSettings.SystemStatus;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                SetLastError(ex.Message);

                return CreateHttpResponse(
                    500,
                    "Internal Server Error",
                    JsonContentType,
                    "{\"error\":\"saveFailed\"}");
            }

            return CreateHttpResponse(
                200,
                "OK",
                JsonContentType,
                CreateSettingsJson(_settings, appTheme));
        }

        private string SaveLayout(string requestBody)
        {
            if (!TryParseLayoutJson(requestBody, out var layout))
            {
                return CreateHttpResponse(
                    400,
                    "Bad Request",
                    JsonContentType,
                    "{\"error\":\"invalidLayout\"}");
            }

            AppSettings before;
            lock (_syncRoot)
            {
                before = SettingsService.Clone(_appSettings);
                _appSettings.SystemStatus.Layout = new List<SystemStatusCardLayoutItem>(layout);
                _settings = _appSettings.SystemStatus;
            }

            try
            {
                lock (_syncRoot)
                {
                    _appSettings = _settingsService.MergeAndSave(_appSettings, before);
                    _settings = _appSettings.SystemStatus;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                SetLastError(ex.Message);

                return CreateHttpResponse(
                    500,
                    "Internal Server Error",
                    JsonContentType,
                    "{\"error\":\"saveFailed\"}");
            }

            return CreateHttpResponse(
                200,
                "OK",
                JsonContentType,
                JsonSerializer.Serialize(new { layout }, StatusJsonOptions));
        }

        private static string GetRequestMethod(string requestLine)
        {
            if (string.IsNullOrWhiteSpace(requestLine))
            {
                return string.Empty;
            }

            var firstSpaceIndex = requestLine.IndexOf(' ');
            return firstSpaceIndex > 0
                ? requestLine[..firstSpaceIndex]
                : requestLine;
        }

        private static int NormalizePort(int preferredPort)
        {
            if (preferredPort < MinPort)
            {
                return MinPort;
            }

            if (preferredPort <= MaxPort)
            {
                return preferredPort;
            }

            return (int)(((long)preferredPort - MinPort) % MaxPort) + MinPort;
        }

        private static async Task DelayAfterAcceptErrorAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(200, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }

        private void SetLastError(string message)
        {
            lock (_syncRoot)
            {
                LastError = message;
            }
        }

        private static string GenerateAuthToken()
        {
            var bytes = new byte[32];
            RandomNumberGenerator.Fill(bytes);

            return Convert.ToHexString(bytes);
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            var leftBytes = Encoding.UTF8.GetBytes(left ?? string.Empty);
            var rightBytes = Encoding.UTF8.GetBytes(right ?? string.Empty);

            return leftBytes.Length == rightBytes.Length &&
                CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
        }

        private sealed class LayoutUpdateRequest
        {
            public List<SystemStatusCardLayoutItem> Layout { get; set; } = new();
        }

        private sealed class ThemeUpdateRequest
        {
            public string WebTheme { get; set; } = string.Empty;
        }

        private sealed class LoginRequest
        {
            public string Password { get; set; } = string.Empty;
        }
    }
}
