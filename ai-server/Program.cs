using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

builder.Services.AddHttpClient("ollama", (services, client) =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    client.Timeout = TimeSpan.FromSeconds(GetClampedInt(configuration["OLLAMA_TIMEOUT_SECONDS"], 60, 5, 180));
});

var app = builder.Build();
var gatewayOptions = GatewayOptions.FromConfiguration(app.Configuration);

app.MapGet("/", () => Results.Ok(new
{
    service = "ai-server",
    status = "ok",
    provider = "ollama",
    model = gatewayOptions.OllamaModel
}));

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    providerBaseUrl = gatewayOptions.OllamaBaseUrl,
    model = gatewayOptions.OllamaModel
}));

app.MapPost("/api/npc/line", async (
    NpcLineRequest request,
    IHttpClientFactory httpClientFactory,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var authResult = RequireSharedSecret(httpContext, gatewayOptions);
    if (authResult is not null)
        return authResult;

    if (string.IsNullOrWhiteSpace(request.CharacterName) || string.IsNullOrWhiteSpace(request.PersonaDescription))
        return Results.BadRequest(new ErrorResponse("characterName and personaDescription are required."));

    try
    {
        var line = await GenerateDialogueLineAsync(
            httpClientFactory.CreateClient("ollama"),
            gatewayOptions,
            BuildNpcSystemPrompt(request),
            BuildNpcUserPrompt(),
            140,
            0.85,
            cancellationToken);

        return Results.Ok(new TextResponse(line));
    }
    catch (GatewayException ex)
    {
        app.Logger.LogWarning("NPC line generation failed: {Message}", ex.Message);
        return Results.Json(new ErrorResponse(ex.Message), statusCode: ex.StatusCode);
    }
});

app.MapPost("/api/blacksmith/offer", async (
    ForgeQuestOfferRequest request,
    IHttpClientFactory httpClientFactory,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var authResult = RequireSharedSecret(httpContext, gatewayOptions);
    if (authResult is not null)
        return authResult;

    if (string.IsNullOrWhiteSpace(request.BlacksmithName) || string.IsNullOrWhiteSpace(request.PersonaSummary))
        return Results.BadRequest(new ErrorResponse("blacksmithName and personaSummary are required."));

    try
    {
        var rawJson = await GenerateRawOllamaTextAsync(
            httpClientFactory.CreateClient("ollama"),
            gatewayOptions,
            BuildForgeOfferSystemPrompt(),
            BuildForgeOfferUserPrompt(request),
            180,
            0.9,
            cancellationToken);

        if (!TryParseForgeQuestOffer(rawJson, out var offer))
            throw new GatewayException("Model response could not be validated as a forge quest offer.", StatusCodes.Status502BadGateway);

        var validatedOffer = offer! with
        {
            MaterialName = SanitizeMaterialName(offer.MaterialName),
            RequestLine = SanitizeDialogueLine(offer.RequestLine)
        };

        if (string.IsNullOrWhiteSpace(validatedOffer.MaterialName) || string.IsNullOrWhiteSpace(validatedOffer.RequestLine))
            throw new GatewayException("Validated forge quest offer was empty after sanitization.", StatusCodes.Status502BadGateway);

        return Results.Ok(validatedOffer);
    }
    catch (GatewayException ex)
    {
        app.Logger.LogWarning("Blacksmith offer generation failed: {Message}", ex.Message);
        return Results.Json(new ErrorResponse(ex.Message), statusCode: ex.StatusCode);
    }
});

app.MapPost("/api/blacksmith/roleplay", async (
    BlacksmithRoleplayRequest request,
    IHttpClientFactory httpClientFactory,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var authResult = RequireSharedSecret(httpContext, gatewayOptions);
    if (authResult is not null)
        return authResult;

    if (string.IsNullOrWhiteSpace(request.BlacksmithName) || string.IsNullOrWhiteSpace(request.PersonaDescription))
        return Results.BadRequest(new ErrorResponse("blacksmithName and personaDescription are required."));

    if (!string.Equals(request.Mode, BlacksmithRoleplayModes.SmallTalk, StringComparison.Ordinal) &&
        !string.Equals(request.Mode, BlacksmithRoleplayModes.TurnIn, StringComparison.Ordinal))
        return Results.BadRequest(new ErrorResponse("mode must be either 'smallTalk' or 'turnIn'."));

    try
    {
        var line = await GenerateDialogueLineAsync(
            httpClientFactory.CreateClient("ollama"),
            gatewayOptions,
            BuildBlacksmithRoleplaySystemPrompt(request),
            BuildBlacksmithRoleplayUserPrompt(request),
            140,
            0.85,
            cancellationToken);

        return Results.Ok(new TextResponse(line));
    }
    catch (GatewayException ex)
    {
        app.Logger.LogWarning("Blacksmith roleplay generation failed: {Message}", ex.Message);
        return Results.Json(new ErrorResponse(ex.Message), statusCode: ex.StatusCode);
    }
});

app.Run();

static async Task<string> GenerateDialogueLineAsync(
    HttpClient httpClient,
    GatewayOptions options,
    string systemPrompt,
    string userPrompt,
    int maxTokens,
    double temperature,
    CancellationToken cancellationToken)
{
    var raw = await GenerateRawOllamaTextAsync(httpClient, options, systemPrompt, userPrompt, maxTokens, temperature, cancellationToken);
    var line = SanitizeDialogueLine(raw);
    if (string.IsNullOrWhiteSpace(line))
        throw new GatewayException("Model returned an empty dialogue line.", StatusCodes.Status502BadGateway);
    return line;
}

static async Task<string> GenerateRawOllamaTextAsync(
    HttpClient httpClient,
    GatewayOptions options,
    string systemPrompt,
    string userPrompt,
    int maxTokens,
    double temperature,
    CancellationToken cancellationToken)
{
    var payload = new OllamaChatRequest(
        options.OllamaModel,
        false,
        false,
        new[]
        {
            new OllamaMessage("system", systemPrompt),
            new OllamaMessage("user", userPrompt)
        },
        new OllamaOptions(maxTokens, temperature));

    using var response = await httpClient.PostAsJsonAsync($"{options.OllamaBaseUrl}/api/chat", payload, cancellationToken);
    var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);

    if (!response.IsSuccessStatusCode)
    {
        var providerError = TryParseProviderError(rawJson);
        var detail = string.IsNullOrWhiteSpace(providerError) ? response.ReasonPhrase ?? "Provider request failed." : providerError;
        throw new GatewayException(detail, StatusCodes.Status502BadGateway);
    }

    var providerResponse = JsonSerializer.Deserialize<OllamaChatResponse>(rawJson, JsonDefaults.Options);
    var content = providerResponse?.Message?.Content;
    if (string.IsNullOrWhiteSpace(content))
        content = providerResponse?.Message?.Thinking;

    if (string.IsNullOrWhiteSpace(content))
        throw new GatewayException("Provider returned an empty response.", StatusCodes.Status502BadGateway);

    return content.Trim();
}

static IResult? RequireSharedSecret(HttpContext context, GatewayOptions options)
{
    if (string.IsNullOrWhiteSpace(options.SharedSecret))
        return null;

    if (!context.Request.Headers.TryGetValue(options.SharedSecretHeader, out var presented) ||
        !string.Equals(presented.ToString(), options.SharedSecret, StringComparison.Ordinal))
        return Results.Json(new ErrorResponse("Unauthorized."), statusCode: StatusCodes.Status401Unauthorized);

    return null;
}

static string BuildNpcSystemPrompt(NpcLineRequest request)
{
    var sb = new System.Text.StringBuilder(512);
    sb.Append("You are ");
    sb.Append(request.CharacterName.Trim());
    sb.Append(", an NPC in the retro fantasy game 'Back to the Forge' (mines, forge, iron ore, risky wilds).");
    sb.AppendLine();
    sb.AppendLine(request.PersonaDescription.Trim());

    if (!string.IsNullOrWhiteSpace(request.LocalKnowledge))
    {
        sb.AppendLine();
        sb.AppendLine("Context you treat as true for your role:");
        sb.AppendLine(request.LocalKnowledge.Trim());
    }

    sb.AppendLine();
    sb.AppendLine(
        "CRITICAL: Write ONLY what this character says out loud in the game, in 1-3 short sentences. " +
        "Direct speech only. Do not explain instructions, do not mention the user, and do not describe the scene as a narrator.");
    return sb.ToString();
}

static string BuildNpcUserPrompt()
{
    return "The traveler is standing with you. Speak your line now: greeting, gossip, warning, or complaint. Nothing else.";
}

static string BuildForgeOfferSystemPrompt()
{
    return
        "You output ONLY valid JSON with exactly two string keys: materialName and requestLine. " +
        "No markdown, no code fences, no extra keys, no commentary. " +
        "materialName must be one invented fantasy ore or mineral name (2-6 words, no quotes inside the string). " +
        "requestLine must be what the blacksmith says aloud asking the traveler to fetch it (1-3 short sentences, direct speech only).";
}

static string BuildForgeOfferUserPrompt(ForgeQuestOfferRequest request)
{
    return
        $"You are {request.BlacksmithName.Trim()}, a blacksmith quest giver.\n" +
        $"Persona: {request.PersonaSummary.Trim()}\n" +
        "The traveler just came to the counter. Output the JSON now for a new mining commission.";
}

static string BuildBlacksmithRoleplaySystemPrompt(BlacksmithRoleplayRequest request)
{
    var sb = new System.Text.StringBuilder(512);
    sb.Append("You are ");
    sb.Append(request.BlacksmithName.Trim());
    sb.AppendLine(", a blacksmith in the fantasy game Back to the Forge.");
    sb.AppendLine(request.PersonaDescription.Trim());

    if (!string.IsNullOrWhiteSpace(request.LocalKnowledge))
    {
        sb.AppendLine();
        sb.AppendLine("Local knowledge you treat as true:");
        sb.AppendLine(request.LocalKnowledge.Trim());
    }

    sb.AppendLine();
    if (string.Equals(request.Mode, BlacksmithRoleplayModes.TurnIn, StringComparison.Ordinal))
    {
        sb.AppendLine("Facts you must follow:");
        sb.AppendLine($"- You asked for a special material called: {request.QuestMaterialName}");
        sb.AppendLine($"- The traveler hands over {request.QuestMineralUnits} unit(s) of that strange ore and {request.IronUnits} unit(s) of standard iron.");
        sb.AppendLine($"- You pay them {request.GoldPaid} gold total for this handoff.");
        sb.AppendLine(
            "Reply with one short in-character line only: grateful and warm if they brought materials, disappointed but fair if not. " +
            "No meta, no JSON, no mention of instructions.");
    }
    else
    {
        sb.AppendLine(
            $"The traveler is here for small talk. You already asked them to fetch \"{request.QuestMaterialName}\". " +
            "Do not repeat the full commission speech. Reply with one or two short casual sentences only.");
    }

    return sb.ToString();
}

static string BuildBlacksmithRoleplayUserPrompt(BlacksmithRoleplayRequest request)
{
    return string.Equals(request.Mode, BlacksmithRoleplayModes.TurnIn, StringComparison.Ordinal)
        ? "Speak your line to the traveler now."
        : "Say your line only.";
}

static bool TryParseForgeQuestOffer(string rawModelText, out ForgeQuestOfferResponse? offer)
{
    offer = null;
    var candidate = StripMarkdownCodeFence(rawModelText).Trim();
    if (string.IsNullOrWhiteSpace(candidate))
        return false;

    try
    {
        offer = JsonSerializer.Deserialize<ForgeQuestOfferResponse>(candidate, JsonDefaults.Options);
    }
    catch (JsonException)
    {
        return false;
    }

    return offer is not null &&
           !string.IsNullOrWhiteSpace(offer.MaterialName) &&
           !string.IsNullOrWhiteSpace(offer.RequestLine);
}

static string StripMarkdownCodeFence(string value)
{
    if (string.IsNullOrWhiteSpace(value))
        return string.Empty;

    var trimmed = value.Trim();
    if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        return trimmed;

    var firstNewline = trimmed.IndexOf('\n');
    if (firstNewline >= 0)
        trimmed = trimmed[(firstNewline + 1)..];

    var closingFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
    if (closingFence >= 0)
        trimmed = trimmed[..closingFence];

    return trimmed.Trim();
}

static string SanitizeMaterialName(string raw)
{
    if (string.IsNullOrWhiteSpace(raw))
        return string.Empty;

    var cleaned = Regex.Replace(raw.Trim(), @"[^A-Za-z0-9 '\-]", " ");
    cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
    if (cleaned.Length > 64)
        cleaned = cleaned[..64].TrimEnd();
    return cleaned;
}

static string SanitizeDialogueLine(string raw)
{
    if (string.IsNullOrWhiteSpace(raw))
        return string.Empty;

    var cleaned = StripInferenceNoise(raw.Trim());
    cleaned = StripPlanningMonologue(cleaned);
    cleaned = cleaned.Replace("\r\n", "\n");
    while (cleaned.Contains("  ", StringComparison.Ordinal))
        cleaned = cleaned.Replace("  ", " ", StringComparison.Ordinal);

    if (cleaned.Length > 400)
        cleaned = cleaned[..400].TrimEnd() + "…";

    return cleaned.Trim();
}

static string StripInferenceNoise(string raw)
{
    var cleaned = Regex.Replace(raw, "<think>[\\s\\S]*?</think>", string.Empty, RegexOptions.IgnoreCase);
    cleaned = Regex.Replace(cleaned, "<thinking>[\\s\\S]*?</thinking>", string.Empty, RegexOptions.IgnoreCase);
    return cleaned.Trim();
}

static string StripPlanningMonologue(string raw)
{
    if (string.IsNullOrWhiteSpace(raw))
        return string.Empty;

    var text = raw.Trim();
    if (!LooksLikeMetaText(text))
        return text;

    var sentences = Regex.Split(text, @"(?<=[.!?])\s+");
    for (var i = sentences.Length - 1; i >= 0; i--)
    {
        var sentence = sentences[i].Trim();
        if (sentence.Length >= 12 && !LooksLikeMetaText(sentence))
            return sentence;
    }

    return text;
}

static bool LooksLikeMetaText(string value)
{
    if (string.IsNullOrWhiteSpace(value))
        return false;

    return Regex.IsMatch(
        value,
        @"\b(the user|user wants|user asked|okay,|ok,|let me |i need to |i'll |i should |wait,|hmm,|respond as|my reply|roleplay|as instructed)\b",
        RegexOptions.IgnoreCase);
}

static string? TryParseProviderError(string rawJson)
{
    if (string.IsNullOrWhiteSpace(rawJson))
        return null;

    try
    {
        var providerError = JsonSerializer.Deserialize<ProviderErrorResponse>(rawJson, JsonDefaults.Options);
        return string.IsNullOrWhiteSpace(providerError?.Error) ? null : providerError.Error.Trim();
    }
    catch (JsonException)
    {
        return null;
    }
}

static int GetClampedInt(string? rawValue, int fallback, int min, int max)
{
    if (!int.TryParse(rawValue, out var parsed))
        return fallback;

    return Math.Clamp(parsed, min, max);
}

sealed record GatewayOptions(
    string OllamaBaseUrl,
    string OllamaModel,
    string SharedSecretHeader,
    string SharedSecret)
{
    public static GatewayOptions FromConfiguration(IConfiguration configuration)
    {
        var baseUrl = configuration["OLLAMA_BASE_URL"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = "http://127.0.0.1:11434";

        var model = configuration["OLLAMA_MODEL"];
        if (string.IsNullOrWhiteSpace(model))
            model = "qwen3:8b";

        var sharedSecretHeader = configuration["AI_SHARED_SECRET_HEADER"];
        if (string.IsNullOrWhiteSpace(sharedSecretHeader))
            sharedSecretHeader = "X-Game-Api-Key";

        return new GatewayOptions(
            baseUrl.Trim().TrimEnd('/'),
            model.Trim(),
            sharedSecretHeader.Trim(),
            configuration["AI_SHARED_SECRET"] ?? string.Empty);
    }
}

sealed record NpcLineRequest(string CharacterName, string PersonaDescription, string LocalKnowledge);
sealed record ForgeQuestOfferRequest(string BlacksmithName, string PersonaSummary);
sealed record BlacksmithRoleplayRequest(
    string Mode,
    string BlacksmithName,
    string PersonaDescription,
    string LocalKnowledge,
    string QuestMaterialName,
    int QuestMineralUnits,
    int IronUnits,
    int GoldPaid);
sealed record TextResponse(string Text);
sealed record ErrorResponse(string Error);
sealed record ForgeQuestOfferResponse(string MaterialName, string RequestLine);
sealed record ProviderErrorResponse(string Error);
sealed record OllamaChatRequest(
    string Model,
    bool Stream,
    bool Think,
    IReadOnlyList<OllamaMessage> Messages,
    OllamaOptions Options);
sealed record OllamaMessage(string Role, string Content);
sealed record OllamaOptions(int NumPredict, double Temperature);
sealed record OllamaChatResponse(OllamaResponseMessage Message);
sealed record OllamaResponseMessage(string Content, string Thinking);

sealed class GatewayException : Exception
{
    public GatewayException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}

static class BlacksmithRoleplayModes
{
    public const string SmallTalk = "smallTalk";
    public const string TurnIn = "turnIn";
}

static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
}
