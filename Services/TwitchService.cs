using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Discord;
using Morpheus.Utilities;

namespace Morpheus.Services;

/// <summary>
/// Thin client over the Twitch Helix API used to resolve streamers and check who is live.
/// Requires <c>TWITCH_CLIENT_ID</c> and <c>TWITCH_CLIENT_SECRET</c> in the environment; when
/// those are absent, <see cref="IsConfigured"/> is false and all calls no-op. Uses the
/// client-credentials (app access token) flow, caching the token until shortly before it expires.
/// </summary>
public class TwitchService(LogsService logsService)
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    private readonly string? _clientId = Env.Get<string?>("TWITCH_CLIENT_ID");
    private readonly string? _clientSecret = Env.Get<string?>("TWITCH_CLIENT_SECRET");

    private string? _accessToken;
    private DateTime _tokenExpiresAt = DateTime.MinValue;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_clientId) && !string.IsNullOrWhiteSpace(_clientSecret);

    public record TwitchUser(string Id, string Login, string DisplayName, string? ProfileImageUrl);
    public record TwitchStream(string Id, string Title);

    /// <summary>Resolves a Twitch login (handle) to its user, or null if not found / not configured.</summary>
    public async Task<TwitchUser?> GetUserAsync(string login, CancellationToken ct = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(login))
            return null;

        string url = $"https://api.twitch.tv/helix/users?login={Uri.EscapeDataString(login.Trim().ToLowerInvariant())}";
        HelixUsersResponse? resp = await SendHelixAsync<HelixUsersResponse>(url, ct);
        UserPayload? u = resp?.Data?.FirstOrDefault();
        if (u == null)
            return null;

        return new TwitchUser(u.Id, u.Login, string.IsNullOrWhiteSpace(u.DisplayName) ? u.Login : u.DisplayName, u.ProfileImageUrl);
    }

    /// <summary>
    /// Returns the currently-live streams for the given user ids, keyed by user id. User ids not
    /// present in the result are offline. Empty if not configured.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, TwitchStream>> GetLiveStreamsAsync(IReadOnlyCollection<string> userIds, CancellationToken ct = default)
    {
        Dictionary<string, TwitchStream> live = new();
        if (!IsConfigured || userIds.Count == 0)
            return live;

        // Helix allows up to 100 user_id params per request.
        foreach (string[] batch in userIds.Distinct().Chunk(100))
        {
            string query = string.Join("&", batch.Select(id => $"user_id={Uri.EscapeDataString(id)}"));
            string url = $"https://api.twitch.tv/helix/streams?{query}";
            HelixStreamsResponse? resp = await SendHelixAsync<HelixStreamsResponse>(url, ct);
            if (resp?.Data == null)
                continue;

            foreach (StreamPayload s in resp.Data)
            {
                if (!string.IsNullOrEmpty(s.UserId) && string.Equals(s.Type, "live", StringComparison.OrdinalIgnoreCase))
                    live[s.UserId] = new TwitchStream(s.Id, s.Title ?? string.Empty);
            }
        }

        return live;
    }

    private async Task<T?> SendHelixAsync<T>(string url, CancellationToken ct) where T : class
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            string? token = await GetTokenAsync(forceRefresh: attempt > 0, ct);
            if (token == null)
                return null;

            using HttpRequestMessage req = new(HttpMethod.Get, url);
            req.Headers.Add("Client-Id", _clientId);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            try
            {
                using HttpResponseMessage resp = await HttpClient.SendAsync(req, ct).ConfigureAwait(false);

                if (resp.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
                    continue; // token likely expired early — refresh and retry once

                if (!resp.IsSuccessStatusCode)
                {
                    string body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    logsService.Log($"Twitch API {(int)resp.StatusCode} for {url}: {body}", LogSeverity.Warning);
                    return null;
                }

                return await resp.Content.ReadFromJsonAsync<T>(cancellationToken: ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logsService.Log($"Twitch API request failed for {url}: {ex.Message}", LogSeverity.Warning);
                return null;
            }
        }

        return null;
    }

    private async Task<string?> GetTokenAsync(bool forceRefresh, CancellationToken ct)
    {
        if (!IsConfigured)
            return null;

        if (!forceRefresh && _accessToken != null && DateTime.UtcNow < _tokenExpiresAt)
            return _accessToken;

        await _tokenLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!forceRefresh && _accessToken != null && DateTime.UtcNow < _tokenExpiresAt)
                return _accessToken;

            string url = "https://id.twitch.tv/oauth2/token";
            Dictionary<string, string> form = new()
            {
                ["client_id"] = _clientId!,
                ["client_secret"] = _clientSecret!,
                ["grant_type"] = "client_credentials"
            };

            using FormUrlEncodedContent content = new(form);
            using HttpResponseMessage resp = await HttpClient.PostAsync(url, content, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                string body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                logsService.Log($"Twitch token request failed: {(int)resp.StatusCode} {body}", LogSeverity.Warning);
                return null;
            }

            TokenResponse? token = await resp.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct).ConfigureAwait(false);
            if (token == null || string.IsNullOrEmpty(token.AccessToken))
                return null;

            _accessToken = token.AccessToken;
            // Refresh a minute early to avoid using an about-to-expire token.
            _tokenExpiresAt = DateTime.UtcNow.Add(CalculateTokenCacheDuration(token.ExpiresIn));
            return _accessToken;
        }
        catch (Exception ex)
        {
            logsService.Log($"Twitch token request exception: {ex.Message}", LogSeverity.Warning);
            return null;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    internal static TimeSpan CalculateTokenCacheDuration(int expiresInSeconds)
    {
        return TimeSpan.FromSeconds(Math.Max(0, expiresInSeconds - 60));
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    }

    private sealed class HelixUsersResponse
    {
        [JsonPropertyName("data")] public List<UserPayload>? Data { get; set; }
    }

    private sealed class UserPayload
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("login")] public string Login { get; set; } = string.Empty;
        [JsonPropertyName("display_name")] public string DisplayName { get; set; } = string.Empty;
        [JsonPropertyName("profile_image_url")] public string? ProfileImageUrl { get; set; }
    }

    private sealed class HelixStreamsResponse
    {
        [JsonPropertyName("data")] public List<StreamPayload>? Data { get; set; }
    }

    private sealed class StreamPayload
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("user_id")] public string UserId { get; set; } = string.Empty;
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("type")] public string? Type { get; set; }
    }
}
