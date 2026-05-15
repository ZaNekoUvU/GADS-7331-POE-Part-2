using UnityEngine;

/// <summary>
/// Local Ollama defaults (<c>http://127.0.0.1:11434</c>). Create via Assets → Create → Back To The Forge → Ollama Dialogue Settings.
/// Run <c>ollama serve</c> and <c>ollama pull &lt;model&gt;</c> before play.
/// </summary>
[CreateAssetMenu(fileName = "OllamaDialogueSettings", menuName = "Back To The Forge/Ollama Dialogue Settings")]
public class OllamaDialogueSettings : ScriptableObject
{
    [Tooltip("No trailing slash. Same machine = 127.0.0.1")]
    [SerializeField] private string hostBaseUrl = "http://127.0.0.1:11434";

    [Tooltip("Must be installed locally, e.g. ollama pull qwen3:8b")]
    [SerializeField] private string model = "qwen3:8b";

    [SerializeField] private int requestTimeoutSeconds = 45;

    [Tooltip("Max tokens for one reply (keep small for snappy RPG lines).")]
    [SerializeField] private int maxTokens = 140;

    [SerializeField] [Range(0.2f, 1.5f)] private float temperature = 0.85f;

    public string HostBaseUrl => hostBaseUrl?.TrimEnd('/') ?? "http://127.0.0.1:11434";
    public string Model => model;
    public int RequestTimeoutSeconds => Mathf.Clamp(requestTimeoutSeconds, 5, 120);
    public int MaxTokens => Mathf.Clamp(maxTokens, 32, 512);
    public float Temperature => temperature;
}
