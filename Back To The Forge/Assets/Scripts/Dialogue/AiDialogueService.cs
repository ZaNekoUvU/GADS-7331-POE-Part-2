using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Abstract AI dialogue gateway used by gameplay systems.
/// Concrete providers can talk to a hosted model, proxy, or another service.
/// </summary>
public abstract class AiDialogueService : MonoBehaviour
{
    public static AiDialogueService Instance { get; private set; }

    public abstract bool IsBusy { get; }

    protected virtual void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    protected virtual void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static AiDialogueService GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        var existing = FindAnyObjectByType<AiDialogueService>();
        if (existing != null)
            return existing;

        var go = new GameObject($"[{nameof(AiDialogueService)}]");
        return go.AddComponent<OllamaDialogueService>();
    }

    public abstract IEnumerator RequestNpcLineCoroutine(
        NpcDialogueProfile profile,
        Action<string> onSuccess,
        Action<string> onError);

    public abstract IEnumerator RequestForgeQuestOfferCoroutine(
        string blacksmithName,
        string personaSummary,
        Action<ForgeQuestOfferDto> onSuccess,
        Action<string> onError);

    public abstract IEnumerator RequestBlacksmithRoleplayLineCoroutine(
        BlacksmithRoleplayRequestDto request,
        Action<string> onSuccess,
        Action<string> onError);
}
