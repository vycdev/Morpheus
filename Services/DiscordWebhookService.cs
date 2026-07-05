using System.Net;
using System.Text;
using System.Text.Json;
using Discord;

namespace Morpheus.Services;

/// <summary>
/// Low-level helper that talks to Discord's webhook REST endpoints directly. It executes a
/// webhook with a per-message username / avatar override (so one webhook can post as "xkcd",
/// a YouTuber, ...) and can check whether a webhook still exists.
/// </summary>
public class DiscordWebhookService(LogsService logsService)
{
    // Discord.Net uses its own HttpClient internally; for raw webhook execution we keep a single
    // shared client, matching how other parts of this codebase create HttpClients.
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
    private static readonly JsonSerializerOptions JsonOptions = new() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    private static string WebhookUrl(ulong webhookId, string token) =>
        $"https://discord.com/api/webhooks/{webhookId}/{token}";

    /// <summary>
    /// Executes a webhook, posting <paramref name="content"/> as <paramref name="username"/>
    /// with the given avatar. Retries once on a 429 rate limit. Returns true on success.
    /// </summary>
    public async Task<bool> SendAsync(ulong webhookId, string token, string content, string username, string? avatarUrl, CancellationToken ct = default)
    {
        var payload = new
        {
            content,
            username,
            avatar_url = avatarUrl,
            // Never ping anyone from an automated feed post.
            allowed_mentions = new { parse = Array.Empty<string>() }
        };

        string json = JsonSerializer.Serialize(payload, JsonOptions);

        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                using StringContent body = new(json, Encoding.UTF8, "application/json");
                using HttpResponseMessage resp = await HttpClient.PostAsync(WebhookUrl(webhookId, token), body, ct).ConfigureAwait(false);

                if (resp.IsSuccessStatusCode)
                    return true;

                if (resp.StatusCode == HttpStatusCode.TooManyRequests && attempt == 0)
                {
                    TimeSpan retryAfter = resp.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(2);
                    if (retryAfter <= TimeSpan.Zero) retryAfter = TimeSpan.FromSeconds(2);
                    await Task.Delay(retryAfter, ct).ConfigureAwait(false);
                    continue;
                }

                string respBody = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                logsService.Log($"Webhook execute failed ({webhookId}): {(int)resp.StatusCode} {resp.StatusCode} - {respBody}", LogSeverity.Warning);
                return false;
            }
            catch (Exception ex)
            {
                logsService.Log($"Webhook execute exception ({webhookId}): {ex.Message}", LogSeverity.Warning);
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks whether a webhook still exists on Discord.
    /// Returns true if it exists, false if it is definitively gone (404), and null if the
    /// state is indeterminate (network error / rate limited) so callers don't delete on a fluke.
    /// </summary>
    public async Task<bool?> CheckExistsAsync(ulong webhookId, string token, CancellationToken ct = default)
    {
        try
        {
            using HttpResponseMessage resp = await HttpClient.GetAsync(WebhookUrl(webhookId, token), ct).ConfigureAwait(false);

            if (resp.IsSuccessStatusCode)
                return true;

            if (resp.StatusCode == HttpStatusCode.NotFound)
                return false;

            // 401/403 => token/webhook invalid; treat as gone so we stop using it.
            if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return false;

            return null;
        }
        catch (Exception ex)
        {
            logsService.Log($"Webhook existence check exception ({webhookId}): {ex.Message}", LogSeverity.Warning);
            return null;
        }
    }
}
