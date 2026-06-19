using System;
using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Calls a local Ollama server (<c>/api/chat</c>, non-streaming). Add one to the scene or it is auto-created by <see cref="NpcOllamaDialogue"/>.
/// </summary>
public class OllamaDialogueService : MonoBehaviour
{
    public static OllamaDialogueService Instance { get; private set; }

    [Tooltip("Optional shared asset; inline fields below apply when this is null.")]
    [SerializeField] private OllamaDialogueSettings projectSettings;

    [Header("Inline defaults (when Project Settings is null)")]
    [SerializeField] private string hostBaseUrl = "http://127.0.0.1:11434";
    [Tooltip("Exact Ollama model tag, e.g. qwen3:8b (must match `ollama list`).")]
    [SerializeField] private string model = "qwen3:8b";
    [SerializeField] private int requestTimeoutSeconds = 45;
    [SerializeField] private int maxTokens = 140;
    [SerializeField] [Range(0.2f, 1.5f)] private float temperature = 0.85f;

    [Header("Debug")]
    [Tooltip("Logs request JSON (truncated) and full errors — turn on if you only see fallback lines.")]
    [SerializeField] private bool logRequestsAndErrors = true;

    private bool _busy;

    public bool IsBusy => _busy;

    private string Host => projectSettings != null ? projectSettings.HostBaseUrl : hostBaseUrl.TrimEnd('/');
    private string Model => projectSettings != null ? projectSettings.Model : model;
    private int Timeout => projectSettings != null ? projectSettings.RequestTimeoutSeconds : requestTimeoutSeconds;
    private int MaxTok => projectSettings != null ? projectSettings.MaxTokens : maxTokens;
    private float Temp => projectSettings != null ? projectSettings.Temperature : temperature;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    /// <summary>Plain chat reply (sanitized like NPC dialogue). Use for turn-in flavor and small talk.</summary>
    public IEnumerator RequestRoleplayLineCoroutine(
        string systemPrompt,
        string userPrompt,
        Action<string> onSuccess,
        Action<string> onError,
        string enforceSpeakerName = null)
    {
        if (_busy)
        {
            onError?.Invoke("busy");
            yield break;
        }

        _busy = true;

        try
        {
            var jsonBody = BuildChatJsonManual(systemPrompt, userPrompt);
            var url = $"{Host}/api/chat";

            if (logRequestsAndErrors)
                Debug.Log($"[Ollama] POST {url} (roleplay line)\n{Truncate(jsonBody, 600)}", this);

            using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = Timeout;

            yield return req.SendWebRequest();

            var raw = req.downloadHandler != null ? req.downloadHandler.text : null;
            var code = req.responseCode;

            if (req.result != UnityWebRequest.Result.Success)
            {
                var detail = string.IsNullOrEmpty(req.error) ? req.result.ToString() : req.error;
                if (logRequestsAndErrors || !string.IsNullOrEmpty(raw))
                    Debug.LogWarning($"[Ollama] HTTP error code={code} {detail}\n{Truncate(raw, 1200)}", this);
                onError?.Invoke($"{detail}");
                yield break;
            }

            var err = TryParseError(raw);
            if (!string.IsNullOrEmpty(err))
            {
                onError?.Invoke(err);
                yield break;
            }

            var content = TryParseAssistantMessage(raw);
            if (string.IsNullOrWhiteSpace(content))
            {
                onError?.Invoke("Empty model reply.");
                yield break;
            }

            onSuccess?.Invoke(ApplySpeakerSanitize(content, enforceSpeakerName));
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>Player talks to a hired mercenary; returns spoken reply plus sentiment and combat effect JSON.</summary>
    public IEnumerator RequestCompanionDialogueCoroutine(
        HireableCompanionOffer offer,
        string playerLine,
        string conversationHistory,
        Action<CompanionDialogueDto> onSuccess,
        Action<string> onError)
    {
        if (offer == null)
        {
            onError?.Invoke("No mercenary offer.");
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
            var characterName = offer.NpcDisplayName;
            var persona = offer.PersonaForLlm;

            var positiveSkill = offer.PositiveMoraleSkill;
            var negativeSkill = offer.NegativeMoraleSkill;

            var systemContent =
                "You are " + characterName + ", a hired mercenary traveling with the player in the retro fantasy game \"Back to the Forge\".\n" +
                "Persona:\n" + persona + "\n\n" +
                DialogueSpeakerNameUtil.IdentityRules(characterName) + "\n\n" +
                "Battle skills tied to how the traveler treats you:\n" +
                "- If their words genuinely encourage you (positive sentiment), you unlock: \"" + positiveSkill.skillName + "\" — " + positiveSkill.description + "\n" +
                "- If they upset or insult you (negative sentiment), you inflict: \"" + negativeSkill.skillName + "\" — " + negativeSkill.description + "\n\n" +
                "The traveler speaks to you directly. Reply in character (1-3 short sentences, direct speech only).\n" +
                "Judge their words against your personality.\n\n" +
                "Output ONLY valid JSON with exactly these string keys:\n" +
                "replyLine — what you say aloud;\n" +
                "sentiment — positive, neutral, or negative;\n" +
                "combatEffect — positive_skill, negative_skill, or none;\n" +
                "effectLabel — must be \"" + positiveSkill.skillName + "\" when positive, \"" + negativeSkill.skillName + "\" when negative, else empty.\n" +
                "No markdown, no code fences, no extra keys, no text before or after the JSON.\n" +
                "Example: {\"replyLine\":\"Fair enough. I'll watch your back.\",\"sentiment\":\"positive\",\"combatEffect\":\"positive_skill\",\"effectLabel\":\"" +
                positiveSkill.skillName + "\"}";

            var userContent = new StringBuilder(512);
            if (!string.IsNullOrWhiteSpace(conversationHistory))
            {
                userContent.AppendLine("Conversation so far:");
                userContent.AppendLine(conversationHistory.Trim());
                userContent.AppendLine();
            }

            userContent.Append("Traveler says now: \"");
            userContent.Append(playerLine.Trim());
            userContent.Append("\". Output the JSON now.");

            var jsonBody = BuildChatJsonManual(systemContent, userContent.ToString(), jsonFormat: true, temperatureOverride: 0.35f);
            var url = $"{Host}/api/chat";

            if (logRequestsAndErrors)
                Debug.Log($"[Ollama] POST {url} (companion dialogue JSON)\n{Truncate(jsonBody, 800)}", this);

            using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = Timeout;

            yield return req.SendWebRequest();

            var raw = req.downloadHandler != null ? req.downloadHandler.text : null;

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(string.IsNullOrEmpty(req.error) ? req.result.ToString() : req.error);
                yield break;
            }

            var apiErr = TryParseError(raw);
            if (!string.IsNullOrEmpty(apiErr))
            {
                onError?.Invoke(apiErr);
                yield break;
            }

            var content = TryParseAssistantMessage(raw);
            if (string.IsNullOrWhiteSpace(content))
            {
                onError?.Invoke("Empty companion JSON.");
                yield break;
            }

            if (!TryParseCompanionDialogue(content, out var dto))
            {
                if (logRequestsAndErrors)
                    Debug.LogWarning($"[Ollama] Bad companion JSON:\n{Truncate(content, 800)}", this);
                onError?.Invoke("Model did not return valid companion JSON.");
                yield break;
            }

            onSuccess?.Invoke(EnforceCompanionDtoNames(dto, characterName));
        }
        finally
        {
            _busy = false;
        }
    }

    private static bool TryParseCompanionDialogue(string modelText, out CompanionDialogueDto dto)
    {
        dto = null;
        var t = StripInferenceNoise(StripMarkdownCodeFence(modelText)).Trim();
        if (string.IsNullOrEmpty(t))
            return false;

        if (!TryExtractJsonObject(t, out var json))
            json = t;

        if (TryDeserializeCompanion(json, out dto))
            return true;

        return TryParseCompanionDialogueLoose(json, out dto);
    }

    private static bool TryDeserializeCompanion(string json, out CompanionDialogueDto dto)
    {
        dto = null;
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            dto = JsonUtility.FromJson<CompanionDialogueDto>(json);
        }
        catch (Exception)
        {
            return false;
        }

        return NormalizeCompanionDto(ref dto);
    }

    private static bool TryParseCompanionDialogueLoose(string text, out CompanionDialogueDto dto)
    {
        dto = new CompanionDialogueDto
        {
            replyLine = MatchJsonStringField(text, "replyLine", "reply_line", "reply"),
            sentiment = MatchJsonStringField(text, "sentiment"),
            combatEffect = MatchJsonStringField(text, "combatEffect", "combat_effect", "effect"),
            effectLabel = MatchJsonStringField(text, "effectLabel", "effect_label", "skill")
        };

        return NormalizeCompanionDto(ref dto);
    }

    private static bool NormalizeCompanionDto(ref CompanionDialogueDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.replyLine))
            return false;

        dto.replyLine = SanitizeLine(dto.replyLine);
        dto.sentiment = dto.sentiment?.Trim() ?? "neutral";
        dto.combatEffect = dto.combatEffect?.Trim() ?? "none";
        dto.effectLabel = dto.effectLabel?.Trim() ?? string.Empty;
        return true;
    }

    private static bool TryExtractJsonObject(string text, out string json)
    {
        json = null;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var start = text.IndexOf('{');
        if (start < 0)
            return false;

        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];
            if (inString)
            {
                if (escaped)
                    escaped = false;
                else if (c == '\\')
                    escaped = true;
                else if (c == '"')
                    inString = false;

                continue;
            }

            if (c == '"')
            {
                inString = true;
                continue;
            }

            if (c == '{')
                depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    json = text.Substring(start, i - start + 1);
                    return true;
                }
            }
        }

        return false;
    }

    private static string MatchJsonStringField(string text, params string[] keys)
    {
        foreach (var key in keys)
        {
            var pattern = "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"";
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (match.Success)
                return UnescapeJsonString(match.Groups[1].Value);
        }

        return null;
    }

    /// <summary>Asks for strict JSON: materialName, requestLine (Ollama-invented ore + blacksmith ask).</summary>
    public IEnumerator RequestForgeQuestOfferCoroutine(
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
            var systemContent =
                "You output ONLY valid JSON with exactly two string keys: materialName and requestLine. " +
                "No markdown, no code fences, no extra keys, no commentary. " +
                "materialName: one invented fantasy ore or mineral name (2–6 words, no quotes inside the string). " +
                "This exact name is what appears in the traveler's inventory when they collect the commission ore. " +
                "requestLine: what the blacksmith says out loud asking the traveler to fetch that same material by name (1–3 short sentences, direct speech, " +
                "same character voice as your persona — no meta, no 'the user').";

            var userContent =
                $"You are {blacksmithName}, a blacksmith quest giver.\nPersona: {personaSummary}\n" +
                "The traveler just came to the counter. Output the JSON now for a new mining commission.";

            var jsonBody = BuildChatJsonManual(systemContent, userContent, jsonFormat: true, temperatureOverride: 0.35f);

            var url = $"{Host}/api/chat";

            if (logRequestsAndErrors)
                Debug.Log($"[Ollama] POST {url} (forge quest JSON)\n{Truncate(jsonBody, 800)}", this);

            using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = Timeout;

            yield return req.SendWebRequest();

            var raw = req.downloadHandler != null ? req.downloadHandler.text : null;

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(string.IsNullOrEmpty(req.error) ? req.result.ToString() : req.error);
                yield break;
            }

            var apiErr = TryParseError(raw);
            if (!string.IsNullOrEmpty(apiErr))
            {
                onError?.Invoke(apiErr);
                yield break;
            }

            var content = TryParseAssistantMessage(raw);
            if (string.IsNullOrWhiteSpace(content))
            {
                onError?.Invoke("Empty quest JSON.");
                yield break;
            }

            if (!TryParseForgeQuestOffer(content, out var dto))
            {
                if (logRequestsAndErrors)
                    Debug.LogWarning($"[Ollama] Bad quest JSON:\n{Truncate(content, 800)}", this);
                onError?.Invoke("Model did not return valid quest JSON.");
                yield break;
            }

            onSuccess?.Invoke(dto);
        }
        finally
        {
            _busy = false;
        }
    }

    private static bool TryParseForgeQuestOffer(string modelText, out ForgeQuestOfferDto dto)
    {
        dto = null;
        var t = StripMarkdownCodeFence(modelText).Trim();
        if (string.IsNullOrEmpty(t))
            return false;

        try
        {
            dto = JsonUtility.FromJson<ForgeQuestOfferDto>(t);
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
        dto.requestLine = dto.requestLine.Trim();
        return true;
    }

    private static string StripMarkdownCodeFence(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return s;

        var t = s.Trim();
        if (!t.StartsWith("```"))
            return t;

        var firstNl = t.IndexOf('\n');
        if (firstNl >= 0)
            t = t.Substring(firstNl + 1);
        var end = t.LastIndexOf("```", StringComparison.Ordinal);
        if (end >= 0)
            t = t.Substring(0, end);
        return t.Trim();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static OllamaDialogueService GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        var existing = FindAnyObjectByType<OllamaDialogueService>();
        if (existing != null)
            return existing;

        var go = new GameObject($"[{nameof(OllamaDialogueService)}]");
        return go.AddComponent<OllamaDialogueService>();
    }

    public IEnumerator RequestNpcLineCoroutine(NpcDialogueProfile profile, Action<string> onSuccess, Action<string> onError)
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
            var systemContent = BuildSystemPrompt(profile);
            var userContent = BuildUserPrompt();
            var jsonBody = BuildChatJsonManual(systemContent, userContent);

            var url = $"{Host}/api/chat";

            if (logRequestsAndErrors)
                Debug.Log($"[Ollama] POST {url}\n{Truncate(jsonBody, 800)}", this);

            using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = Timeout;

            yield return req.SendWebRequest();

            var raw = req.downloadHandler != null ? req.downloadHandler.text : null;
            var code = req.responseCode;

            if (req.result != UnityWebRequest.Result.Success)
            {
                var detail = string.IsNullOrEmpty(req.error) ? req.result.ToString() : req.error;
                if (logRequestsAndErrors || !string.IsNullOrEmpty(raw))
                    Debug.LogWarning($"[Ollama] HTTP error code={code} {detail}\n{Truncate(raw, 1200)}", this);
                onError?.Invoke($"HTTP {code}: {detail}. Body: {Truncate(raw, 200)}");
                yield break;
            }

            var err = TryParseError(raw);
            if (!string.IsNullOrEmpty(err))
            {
                if (logRequestsAndErrors)
                    Debug.LogWarning($"[Ollama] API error: {err}", this);
                onError?.Invoke(err);
                yield break;
            }

            var content = TryParseAssistantMessage(raw);
            if (string.IsNullOrWhiteSpace(content))
            {
                if (logRequestsAndErrors)
                    Debug.LogWarning($"[Ollama] Unparseable or empty reply. Raw:\n{Truncate(raw, 1200)}", this);
                onError?.Invoke("Empty model reply (see Console with Debug logging on).");
                yield break;
            }

            onSuccess?.Invoke(ApplySpeakerSanitize(content, profile.CharacterName));
        }
        finally
        {
            _busy = false;
        }
    }

    private static string BuildSystemPrompt(NpcDialogueProfile p)
    {
        var sb = new StringBuilder(512);
        sb.Append("You are ");
        sb.Append(p.CharacterName);
        sb.Append(", an NPC in the retro fantasy game 'Back to the Forge' (mines, forge, iron ore, risky wilds). ");
        sb.AppendLine();
        sb.AppendLine(p.PersonaDescription.Trim());

        var local = p.LocalKnowledge.Trim();
        if (!string.IsNullOrEmpty(local))
        {
            sb.AppendLine();
            sb.AppendLine("Context you treat as true for your role:");
            sb.AppendLine(local);
        }

        sb.AppendLine();
        sb.AppendLine(DialogueSpeakerNameUtil.IdentityRules(p.CharacterName));
        sb.AppendLine(
            "CRITICAL — You write ONLY what this character says out loud in the game, 1-3 short sentences. " +
            "Direct speech only. Do NOT plan, explain, or discuss instructions. Do NOT say: the user, okay, let me think, I need to, I should, " +
            "wait, hmm, respond as, my reply, or anything about roleplaying or prompts. Never describe the scene from a writer's perspective. " +
            "Start immediately with words spoken to the traveler.");
        return sb.ToString();
    }

    private static string BuildUserPrompt()
    {
        return
            "The traveler is standing with you. Speak your line now — only the words your character says aloud (greeting, gossip, warning, or complaint). " +
            "Nothing else. No preamble.";
    }

    /// <summary>Hand-built JSON avoids Unity JsonUtility quirks with nested message arrays.</summary>
    private string BuildChatJsonManual(string systemContent, string userContent, bool jsonFormat = false, float? temperatureOverride = null)
    {
        var sb = new StringBuilder(1024 + systemContent.Length + userContent.Length);
        sb.Append("{\"model\":\"").Append(EscapeJson(Model)).Append("\",");
        if (jsonFormat)
            sb.Append("\"format\":\"json\",");
        sb.Append("\"stream\":false,");
        sb.Append("\"think\":false,");
        sb.Append("\"messages\":[");
        sb.Append("{\"role\":\"system\",\"content\":\"").Append(EscapeJson(systemContent)).Append("\"},");
        sb.Append("{\"role\":\"user\",\"content\":\"").Append(EscapeJson(userContent)).Append("\"}");
        sb.Append("],\"options\":{");
        sb.Append("\"num_predict\":").Append(MaxTok).Append(',');
        var temp = temperatureOverride ?? Temp;
        sb.Append("\"temperature\":").Append(temp.ToString(CultureInfo.InvariantCulture));
        sb.Append("}}");
        return sb.ToString();
    }

    private static string EscapeJson(string s)
    {
        if (string.IsNullOrEmpty(s))
            return string.Empty;

        return s.Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s))
            return "(empty)";
        if (s.Length <= max)
            return s;
        return s.Substring(0, max) + "…";
    }

    private static string TryParseError(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return null;

        try
        {
            var err = JsonUtility.FromJson<OllamaErrorResponseDto>(rawJson);
            if (err != null && !string.IsNullOrEmpty(err.error))
                return err.error;
        }
        catch (Exception)
        {
            // ignored
        }

        return null;
    }

    private static string TryParseAssistantMessage(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return null;

        try
        {
            var res = JsonUtility.FromJson<OllamaChatResponseDto>(rawJson);
            var msg = res?.message;
            var text = msg?.content?.Trim();
            if (!string.IsNullOrEmpty(text))
                return StripInferenceNoise(text);

            var think = msg?.thinking?.Trim();
            if (!string.IsNullOrEmpty(think))
                return StripInferenceNoise(think);
        }
        catch (Exception)
        {
            // fall through to regex
        }

        // Fallback if JsonUtility failed (rare fields / ordering)
        var extracted = TryExtractContentWithRegex(rawJson);
        return string.IsNullOrEmpty(extracted) ? null : StripInferenceNoise(extracted);
    }

    private static string TryExtractContentWithRegex(string raw)
    {
        // Non-greedy content between "content":" and closing quote (escaped quotes inside are best-effort).
        var m = Regex.Match(raw, "\"content\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"", RegexOptions.Singleline);
        if (m.Success)
            return UnescapeJsonString(m.Groups[1].Value);
        return null;
    }

    private static string UnescapeJsonString(string s)
    {
        if (string.IsNullOrEmpty(s))
            return s;

        // Captured substring from JSON string; expand common escapes (order matters).
        const char esc = '\uE000';
        return s.Replace("\\\\", esc.ToString())
            .Replace("\\\"", "\"")
            .Replace("\\n", "\n")
            .Replace("\\r", "\r")
            .Replace("\\t", "\t")
            .Replace(esc.ToString(), "\\");
    }

    private static string StripInferenceNoise(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return raw;

        var t = raw;
        // Common Qwen / reasoning-style wrappers Ollama may echo inside content
        t = Regex.Replace(t, "<think>[\\s\\S]*?</think>", string.Empty, RegexOptions.IgnoreCase);
        t = Regex.Replace(t, "<thinking>[\\s\\S]*?</thinking>", string.Empty, RegexOptions.IgnoreCase);
        return t.Trim();
    }

    private static string ApplySpeakerSanitize(string content, string enforceSpeakerName)
    {
        var line = SanitizeLine(content);
        if (string.IsNullOrWhiteSpace(enforceSpeakerName))
            return line;

        return DialogueSpeakerNameUtil.Enforce(line, enforceSpeakerName);
    }

    private static CompanionDialogueDto EnforceCompanionDtoNames(CompanionDialogueDto dto, string characterName)
    {
        if (dto == null)
            return null;

        if (!string.IsNullOrWhiteSpace(dto.replyLine) && !string.IsNullOrWhiteSpace(characterName))
            dto.replyLine = DialogueSpeakerNameUtil.Enforce(dto.replyLine, characterName);

        return dto;
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

    /// <summary>Removes planning monologue if the model still echoes meta-text (backup when think=false is ignored).</summary>
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
            var L = line.Trim();
            if (string.IsNullOrEmpty(L))
                continue;
            if (!started && LooksLikeMetaLine(L))
                continue;
            started = true;
            if (sb.Length > 0)
                sb.Append(' ');
            sb.Append(L);
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
        if (lower.Contains("respond as") || lower.Contains("traveler stops to talk") || lower.Contains("user wants"))
            return true;
        return false;
    }
}
