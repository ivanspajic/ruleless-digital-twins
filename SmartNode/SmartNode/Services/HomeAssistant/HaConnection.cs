namespace SmartNode.Services.HomeAssistant;

/// <summary>Immutable, internally-consistent snapshot of the HA connection at one instant.</summary>
public sealed record HaSnapshot(string Url, string? Token, bool TokenSet, bool LastConnected, string Source);

/// <summary>
/// Process-wide Home Assistant connection config: URL + long-lived token.
/// Seeded from HA_URL / TOKEN_HA at startup; overwritten in RAM only after a
/// successful probe (P4-A setup wizard). The token lives in memory only — it is
/// never written to disk, never logged, and never returned by any API.
/// </summary>
public sealed class HaConnection
{
    private readonly object _gate = new();
    private string _url;
    private string? _token;
    private bool _lastConnected;
    private string _source;

    public HaConnection(string seedUrl, string? seedToken)
    {
        _url = Normalize(seedUrl);
        _token = string.IsNullOrWhiteSpace(seedToken) ? null : seedToken;
        _lastConnected = false;
        _source = "env";
    }

    public string Url { get { lock (_gate) return _url; } }
    public string? Token { get { lock (_gate) return _token; } }
    public bool TokenSet { get { lock (_gate) return !string.IsNullOrWhiteSpace(_token); } }
    public bool LastConnected { get { lock (_gate) return _lastConnected; } }
    public string Source { get { lock (_gate) return _source; } }

    /// <summary>
    /// Atomically read all fields under a single lock. Use this wherever URL and
    /// token must be consistent with each other (e.g. building an authenticated
    /// HttpClient), so a concurrent <see cref="Update"/> cannot pair a new token
    /// with an old URL.
    /// </summary>
    public HaSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new HaSnapshot(_url, _token, !string.IsNullOrWhiteSpace(_token), _lastConnected, _source);
        }
    }

    /// <summary>Replace URL + token after a verified-good connection.</summary>
    public void Update(string url, string token, bool connected)
    {
        lock (_gate)
        {
            _url = Normalize(url);
            _token = token;
            _lastConnected = connected;
            _source = "runtime";
        }
    }

    /// <summary>Enforce a trailing slash so HttpClient.BaseAddress keeps the last path segment.</summary>
    public static string Normalize(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) url = "http://localhost:8123/";
        url = url.Trim();
        return url.EndsWith("/") ? url : url + "/";
    }
}
