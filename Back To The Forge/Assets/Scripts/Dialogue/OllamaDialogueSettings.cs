using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Shared AI gateway settings for shipped builds and local development.
/// Unity talks to this server, and the server owns provider/model details.
/// </summary>
[CreateAssetMenu(fileName = "AiServerSettings", menuName = "Back To The Forge/AI Server Settings")]
public class OllamaDialogueSettings : ScriptableObject
{
    [Tooltip("Public base URL for your AI gateway. No trailing slash.")]
    [FormerlySerializedAs("hostBaseUrl")]
    [SerializeField] private string apiBaseUrl = "https://your-ai-server.example.com";

    [Tooltip("Optional auth header sent to the AI gateway.")]
    [SerializeField] private string apiKeyHeaderName = "X-Game-Api-Key";

    [Tooltip("Optional shared secret or token for the AI gateway.")]
    [SerializeField] private string apiKey = "";

    [SerializeField] private int requestTimeoutSeconds = 45;

    [SerializeField] private string npcLineEndpoint = "/api/npc/line";
    [SerializeField] private string blacksmithRoleplayEndpoint = "/api/blacksmith/roleplay";
    [SerializeField] private string blacksmithOfferEndpoint = "/api/blacksmith/offer";

    [SerializeField] private bool logRequestsAndErrors = true;

    public string ApiBaseUrl => string.IsNullOrWhiteSpace(apiBaseUrl)
        ? string.Empty
        : apiBaseUrl.Trim().TrimEnd('/');

    public string ApiKeyHeaderName => string.IsNullOrWhiteSpace(apiKeyHeaderName)
        ? "X-Game-Api-Key"
        : apiKeyHeaderName.Trim();

    public string ApiKey => apiKey ?? string.Empty;
    public int RequestTimeoutSeconds => Mathf.Clamp(requestTimeoutSeconds, 5, 120);
    public string NpcLineEndpoint => NormalizePath(npcLineEndpoint, "/api/npc/line");
    public string BlacksmithRoleplayEndpoint => NormalizePath(blacksmithRoleplayEndpoint, "/api/blacksmith/roleplay");
    public string BlacksmithOfferEndpoint => NormalizePath(blacksmithOfferEndpoint, "/api/blacksmith/offer");
    public bool LogRequestsAndErrors => logRequestsAndErrors;

    private static string NormalizePath(string path, string fallback)
    {
        if (string.IsNullOrWhiteSpace(path))
            return fallback;

        var trimmed = path.Trim();
        return trimmed.StartsWith("/") ? trimmed : "/" + trimmed;
    }
}
