using System;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;

/// <summary>
/// Legacy component name kept for scene compatibility.
/// The client now talks to a hosted AI gateway instead of a local Ollama runtime.
/// </summary>
public class OllamaDialogueService : AiDialogueService
{
    [Tooltip("Optional shared asset; inline fields below apply when this is null.")]
    [SerializeField] private OllamaDialogueSettings projectSettings;

    [Header("Inline defaults (when Project Settings is null)")]
    [FormerlySerializedAs("hostBaseUrl")]
    [SerializeField] private string apiBaseUrl = "https://your-ai-server.example.com";
    [SerializeField] private string apiKeyHeaderName = "X-Game-Api-Key";
    [SerializeField] private string apiKey = "";
    [SerializeField] private int requestTimeoutSeconds = 45;
    [SerializeField] private string npcLineEndpoint = "/api/npc/line";
    [SerializeField] private string blacksmithRoleplayEndpoint = "/api/blacksmith/roleplay";
    [SerializeField] private string blacksmithOfferEndpoint = "/api/blacksmith/offer";

    [Header("Debug")]
    [Tooltip("Logs request JSON (truncated) and API errors when the gateway is unavailable or misconfigured.")]
    [SerializeField] private bool logRequestsAndErrors = true;

    private bool _busy;

    public override bool IsBusy => _busy;

    private string ApiBaseUrl => projectSettings != null
        ? projectSettings.ApiBaseUrl
        : NormalizeBaseUrl(apiBaseUrl);

    private string ApiKeyHeaderName => projectSettings != null
        ? projectSettings.ApiKeyHeaderName
        : NormalizeHeader(apiKeyHeaderName, "X-Game-Api-Key");

    private string ApiKey => projectSettings != null ? projectSettings.ApiKey : apiKey ?? string.Empty;
    private int Timeout => projectSettings != null ? projectSettings.RequestTimeoutSeconds : Mathf.Clamp(requestTimeoutSeconds, 5, 120);
    private string NpcLineEndpoint => projectSettings != null ? projectSettings.NpcLineEndpoint : NormalizePath(npcLineEndpoint, "/api/npc/line");
    private string BlacksmithRoleplayEndpoint => projectSettings != null ? projectSettings.BlacksmithRoleplayEndpoint : NormalizePath(blacksmithRoleplayEndpoint, "/api/blacksmith/roleplay");
    private string BlacksmithOfferEndpoint => projectSettings != null ? projectSettings.BlacksmithOfferEndpoint : NormalizePath(blacksmithOfferEndpoint, "/api/blacksmith/offer");
    private bool LogRequestsAndErrors => projectSettings != null ? projectSettings.LogRequestsAndErrors : logRequestsAndErrors;

    public override IEnumerator RequestNpcLineCoroutine(NpcDialogueProfile profile, Action<string> onSuccess, Action<string> onError)
    {
        if (profile == null)
        {
            onError?.Invoke("No NPC profile assigned.");
            yield break;
        }

        if (_busy)
        {
            onError?.Invoke("busy");
            yield break;
        }

        _busy = true;

        try
        {
            var request = new AiNpcLineRequestDto
            {
                characterName = profile.CharacterName,
                personaDescription = profile.PersonaDescription,
                localKnowledge = profile.LocalKnowledge
            };

            string raw = null;
            string err = null;
            yield return StartCoroutine(SendJsonRequestCoroutine(
                NpcLineEndpoint,
                JsonUtility.ToJson(request),
                "npc line",
                body => raw = body,
                e => err = e));

            if (!string.IsNullOrWhiteSpace(err))
            {
                onError?.Invoke(err);
                yield break;
            }

            var content = TryParseText(raw);
            if (string.IsNullOrWhiteSpace(content))
            {
                onError?.Invoke("AI gateway returned an empty NPC reply.");
                yield break;
            }

            onSuccess?.Invoke(SanitizeLine(content));
        }
        finally
        {
            _busy = false;
        }
    }

    public override IEnumerator RequestForgeQuestOfferCoroutine(
        string blacksmithName,
        string personaSummary,
        Action<ForgeQuestOfferDto> onSuccess,
        Action<string> onError)
    {
        if (_busy)
        {
            onError?.Invoke("busy");
            yield break;
        }

        _busy = true;

        try
        {
            var request = new ForgeQuestOfferRequestDto
            {
                blacksmithName = blacksmithName,
                personaSummary = personaSummary
            };

            string raw = null;
            string err = null;
            yield return StartCoroutine(SendJsonRequestCoroutine(
                BlacksmithOfferEndpoint,
                JsonUtility.ToJson(request),
                "blacksmith offer",
                body => raw = body,
                e => err = e));

            if (!string.IsNullOrWhiteSpace(err))
            {
                onError?.Invoke(err);
                yield break;
            }

            if (!TryParseForgeQuestOffer(raw, out var dto))
            {
                onError?.Invoke("AI gateway returned invalid quest data.");
                yield break;
            }

            onSuccess?.Invoke(dto);
        }
        finally
        {
            _busy = false;
        }
    }

    public override IEnumerator RequestBlacksmithRoleplayLineCoroutine(
        BlacksmithRoleplayRequestDto request,
        Action<string> onSuccess,
        Action<string> onError)
    {
        if (request == null)
        {
            onError?.Invoke("Missing blacksmith roleplay request.");
            yield break;
        }

        if (_busy)
        {
            onError?.Invoke("busy");
            yield break;
        }

        _busy = true;

        try
        {
            string raw = null;
            string err = null;
            yield return StartCoroutine(SendJsonRequestCoroutine(
                BlacksmithRoleplayEndpoint,
                JsonUtility.ToJson(request),
                $"blacksmith roleplay ({request.mode})",
                body => raw = body,
                e => err = e));

            if (!string.IsNullOrWhiteSpace(err))
            {
                onError?.Invoke(err);
                yield break;
            }

            var content = TryParseText(raw);
            if (string.IsNullOrWhiteSpace(content))
            {
                onError?.Invoke("AI gateway returned an empty blacksmith reply.");
                yield break;
            }

            onSuccess?.Invoke(SanitizeLine(content));
        }
        finally
        {
            _busy = false;
        }
    }

    private IEnumerator SendJsonRequestCoroutine(
        string endpointPath,
        string jsonBody,
        string requestLabel,
        Action<string> onSuccess,
        Action<string> onError)
    {
        var url = BuildUrl(endpointPath);
        if (string.IsNullOrWhiteSpace(url))
        {
            onError?.Invoke("AI gateway URL is not configured.");
            yield break;
        }

        if (LogRequestsAndErrors)
            Debug.Log($"[AI Gateway] POST {url} ({requestLabel})\n{Truncate(jsonBody, 800)}", this);

        using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        ApplyAuthHeader(req);
        req.timeout = Timeout;

        yield return req.SendWebRequest();

        var raw = req.downloadHandler != null ? req.downloadHandler.text : null;
        var code = req.responseCode;

        if (req.result != UnityWebRequest.Result.Success)
        {
            var detail = TryParseError(raw);
            if (string.IsNullOrWhiteSpace(detail))
                detail = string.IsNullOrWhiteSpace(req.error) ? req.result.ToString() : req.error;

            if (LogRequestsAndErrors || !string.IsNullOrWhiteSpace(raw))
                Debug.LogWarning($"[AI Gateway] HTTP error code={code}: {detail}\n{Truncate(raw, 1200)}", this);

            onError?.Invoke($"HTTP {code}: {detail}");
            yield break;
        }

        var apiErr = TryParseError(raw);
        if (!string.IsNullOrWhiteSpace(apiErr))
        {
            if (LogRequestsAndErrors)
                Debug.LogWarning($"[AI Gateway] API error: {apiErr}", this);
            onError?.Invoke(apiErr);
            yield break;
        }

        onSuccess?.Invoke(raw);
    }

    private void ApplyAuthHeader(UnityWebRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(ApiKey))
            return;

        request.SetRequestHeader(ApiKeyHeaderName, ApiKey);
    }

    private string BuildUrl(string endpointPath)
    {
        if (string.IsNullOrWhiteSpace(ApiBaseUrl))
            return null;

        return ApiBaseUrl + NormalizePath(endpointPath, "/");
    }

    private static bool TryParseForgeQuestOffer(string rawJson, out ForgeQuestOfferDto dto)
    {
        dto = null;
        if (string.IsNullOrWhiteSpace(rawJson))
            return false;

        try
        {
            dto = JsonUtility.FromJson<ForgeQuestOfferDto>(rawJson);
        }
        catch (Exception)
        {
            return false;
        }

        if (dto == null ||
            string.IsNullOrWhiteSpace(dto.materialName) ||
            string.IsNullOrWhiteSpace(dto.requestLine))
            return false;

        dto.materialName = dto.materialName.Trim();
        dto.requestLine = SanitizeLine(dto.requestLine);
        return true;
    }

    private static string TryParseText(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return null;

        try
        {
            var dto = JsonUtility.FromJson<AiTextResponseDto>(rawJson);
            return string.IsNullOrWhiteSpace(dto?.text) ? null : dto.text.Trim();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string TryParseError(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return null;

        try
        {
            var dto = JsonUtility.FromJson<AiErrorResponseDto>(rawJson);
            return string.IsNullOrWhiteSpace(dto?.error) ? null : dto.error.Trim();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string NormalizeBaseUrl(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().TrimEnd('/');
    }

    private static string NormalizeHeader(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string NormalizePath(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var trimmed = value.Trim();
        return trimmed.StartsWith("/") ? trimmed : "/" + trimmed;
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s))
            return "(empty)";
        if (s.Length <= max)
            return s;
        return s.Substring(0, max) + "…";
    }

    private static string SanitizeLine(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return raw;

        var t = StripPlanningMonologue(raw.Trim());
        t = t.Replace("\r\n", "\n");
        while (t.Contains("  "))
            t = t.Replace("  ", " ");

        const int hardMax = 400;
        if (t.Length > hardMax)
            t = t.Substring(0, hardMax).TrimEnd() + "…";

        return t;
    }

    private static string StripPlanningMonologue(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return raw;

        var t = raw.Trim();
        var paras = t.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
        if (paras.Length >= 2)
        {
            var last = paras[^1].Trim().Replace("\n", " ");
            if (last.Length >= 12 && !LooksLikeMetaText(last))
                return last;
        }

        if (LooksLikeMetaText(t))
        {
            var sentences = Regex.Split(t, @"(?<=[.!?])\s+");
            for (var i = sentences.Length - 1; i >= 0; i--)
            {
                var s = sentences[i].Trim();
                if (s.Length < 12)
                    continue;
                if (!LooksLikeMetaText(s))
                    return s;
            }
        }

        var lines = t.Split('\n');
        var sb = new StringBuilder();
        var started = false;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;
            if (!started && LooksLikeMetaLine(trimmed))
                continue;
            started = true;
            if (sb.Length > 0)
                sb.Append(' ');
            sb.Append(trimmed);
        }

        var joined = sb.ToString().Trim();
        return string.IsNullOrEmpty(joined) ? t : joined;
    }

    private static bool LooksLikeMetaText(string chunk)
    {
        if (string.IsNullOrEmpty(chunk))
            return false;

        return Regex.IsMatch(
            chunk,
            @"\b(the user|user wants|user asked|okay,|ok,|let me |i need to |i'll |i should |wait,|hmm,|hmm\.|respond as|my reply|roleplay|in character as|as instructed)\b",
            RegexOptions.IgnoreCase);
    }

    private static bool LooksLikeMetaLine(string line)
    {
        if (string.IsNullOrEmpty(line))
            return false;

        var lower = line.ToLowerInvariant();
        if (lower.Contains("the user"))
            return true;
        if (lower.StartsWith("okay") || lower.StartsWith("ok,"))
            return true;
        if (lower.StartsWith("let me "))
            return true;
        if (lower.StartsWith("i need to "))
            return true;
        if (lower.StartsWith("i'll ") || lower.StartsWith("i should "))
            return true;
        if (lower.StartsWith("wait,"))
            return true;
        if (lower.StartsWith("hmm"))
            return true;
        if (lower.Contains("respond as") || lower.Contains("user wants"))
            return true;
        return false;
    }
}
