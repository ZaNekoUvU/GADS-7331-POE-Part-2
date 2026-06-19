using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Bottom-right pickup feed for items and gold (FF-style hud panels).
/// </summary>
[DisallowMultipleComponent]
public sealed class PickupLogUI : MonoBehaviour
{
    public static PickupLogUI Instance { get; private set; }

    private const int UiSortOrder = 4430;
    private const float EntryLifetimeSeconds = 3.5f;
    private const int MaxVisibleEntries = 6;

    private UIDocument _document;
    private VisualElement _entryList;
    private Inventory _inventory;
    private BlacksmithMaster _economy;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreateIfMissing()
    {
        if (Instance != null || FindAnyObjectByType<PickupLogUI>() != null)
            return;

        var go = new GameObject($"[{nameof(PickupLogUI)}]");
        go.AddComponent<PickupLogUI>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUi();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        RebindSubscriptions();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnbindSubscriptions();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UnbindSubscriptions();
        RebindSubscriptions();
    }

    public void LogItem(ItemDefinition item, int amount)
    {
        if (item == null || amount <= 0 || !ShouldShow())
            return;

        var name = string.IsNullOrWhiteSpace(item.DisplayName) ? "Item" : item.DisplayName.Trim();
        EnqueueEntry($"+{amount} {name}");
    }

    public void LogGold(int amount)
    {
        if (amount <= 0 || !ShouldShow())
            return;

        EnqueueEntry($"+{amount} Gold");
    }

    private void LateUpdate()
    {
        if (_inventory == null)
            RebindSubscriptions();
    }

    private void RebindSubscriptions()
    {
        UnbindSubscriptions();

        var player = PlayerMovement2D.Instance ?? FindAnyObjectByType<PlayerMovement2D>();
        if (player != null && player.TryGetComponent(out Inventory inv))
        {
            inv.OnItemAdded -= OnItemAdded;
            inv.OnItemAdded += OnItemAdded;
            _inventory = inv;
        }

        _economy = BlacksmithMaster.ResolveEconomy();
        if (_economy != null)
        {
            _economy.OnGoldAdded -= OnGoldAdded;
            _economy.OnGoldAdded += OnGoldAdded;
        }
    }

    private void UnbindSubscriptions()
    {
        if (_inventory != null)
            _inventory.OnItemAdded -= OnItemAdded;
        _inventory = null;

        if (_economy != null)
            _economy.OnGoldAdded -= OnGoldAdded;
        _economy = null;
    }

    private void OnItemAdded(ItemDefinition item, int amount) => LogItem(item, amount);

    private void OnGoldAdded(int amount) => LogGold(amount);

    private void EnqueueEntry(string message)
    {
        if (_entryList == null)
            BuildUi();

        var entry = FfStyleMenuUi.BuildPickupLogEntry(message);
        _entryList.Add(entry);

        while (_entryList.childCount > MaxVisibleEntries)
            _entryList.RemoveAt(0);

        StartCoroutine(RemoveEntryAfterDelay(entry));
    }

    private IEnumerator RemoveEntryAfterDelay(VisualElement entry)
    {
        yield return new WaitForSecondsRealtime(EntryLifetimeSeconds);

        if (entry != null && entry.parent == _entryList)
            _entryList.Remove(entry);
    }

    private bool ShouldShow()
    {
        if (PauseMenuController.IsOpen)
            return false;

        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name == PauseMenuController.DefaultMainMenuSceneName)
            return false;

        return true;
    }

    private void BuildUi()
    {
        _document = GetComponent<UIDocument>();
        if (_document == null)
            _document = gameObject.AddComponent<UIDocument>();

        FfStyleMenuUi.ConfigureDocument(_document, UiSortOrder);
        FfStyleMenuUi.BuildPickupLogRoot(_document.rootVisualElement, out _entryList);
    }
}
