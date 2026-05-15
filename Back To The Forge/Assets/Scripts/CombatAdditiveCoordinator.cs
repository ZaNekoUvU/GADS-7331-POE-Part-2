using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Lives in the exploration scene. Loads combat additively, hides exploration (cameras, listeners,
/// renderers, canvases) and optionally sets Time.timeScale to 0 while combat is shown.
/// </summary>
public class CombatAdditiveCoordinator : MonoBehaviour
{
    [SerializeField] private string combatSceneName = "Combat Scene";

    [Tooltip("Pauses scaled time while combat is shown. Combat UI/logic must use unscaled time (e.g. Animator Update Mode Unscaled, WaitForSecondsRealtime, UI Toolkit unscaled).")]
    [SerializeField] private bool pauseExplorationWithTimeScale = true;

    [Header("Victory loot (after combat)")]
    [Tooltip("If empty, finds an Inventory in loaded scenes. Usually the player.")]
    [SerializeField] private Inventory playerInventory;

    [Tooltip("Random picks (with replacement) for each of 1–3 drops after a won fight.")]
    [SerializeField] private ItemDefinition[] combatDropPool;

    [SerializeField] private int minDropsOnVictory = 1;
    [SerializeField] private int maxDropsOnVictory = 3;

    private AsyncOperation _loadOp;
    private float _savedTimeScale = 1f;

    private Camera[] _explorationCameras;
    private bool[] _explorationCamerasWereEnabled;
    private AudioListener[] _explorationListeners;
    private bool[] _explorationListenersWereEnabled;
    private Renderer[] _explorationRenderers;
    private bool[] _explorationRenderersWereEnabled;
    private Canvas[] _explorationCanvases;
    private bool[] _explorationCanvasesWereEnabled;

    /// <summary>True while the combat scene is loading or already loaded.</summary>
    public bool IsCombatActiveOrLoading
    {
        get
        {
            if (_loadOp != null)
                return true;

            if (string.IsNullOrEmpty(combatSceneName))
                return false;

            var scene = SceneManager.GetSceneByName(combatSceneName);
            return scene.IsValid() && scene.isLoaded;
        }
    }

    private void Awake()
    {
        CacheExplorationSceneOutput();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    /// <summary>Snapshots cameras/listeners in this GameObject's scene (exploration). Call before combat if you add cameras at runtime.</summary>
    public void RefreshExplorationOutputCache()
    {
        CacheExplorationSceneOutput();
    }

    private void CacheExplorationSceneOutput()
    {
        var scene = gameObject.scene;
        if (!scene.IsValid())
            return;

        var cameras = new List<Camera>();
        var listeners = new List<AudioListener>();
        var renderers = new List<Renderer>();
        var canvases = new List<Canvas>();

        foreach (var root in scene.GetRootGameObjects())
        {
            cameras.AddRange(root.GetComponentsInChildren<Camera>(true));
            listeners.AddRange(root.GetComponentsInChildren<AudioListener>(true));
            renderers.AddRange(root.GetComponentsInChildren<Renderer>(true));
            canvases.AddRange(root.GetComponentsInChildren<Canvas>(true));
        }

        _explorationCameras = cameras.ToArray();
        _explorationCamerasWereEnabled = new bool[_explorationCameras.Length];
        for (var i = 0; i < _explorationCameras.Length; i++)
            _explorationCamerasWereEnabled[i] = _explorationCameras[i] != null && _explorationCameras[i].enabled;

        _explorationListeners = listeners.ToArray();
        _explorationListenersWereEnabled = new bool[_explorationListeners.Length];
        for (var i = 0; i < _explorationListeners.Length; i++)
            _explorationListenersWereEnabled[i] = _explorationListeners[i] != null && _explorationListeners[i].enabled;

        _explorationRenderers = renderers.ToArray();
        _explorationRenderersWereEnabled = new bool[_explorationRenderers.Length];
        for (var i = 0; i < _explorationRenderers.Length; i++)
            _explorationRenderersWereEnabled[i] = _explorationRenderers[i] != null && _explorationRenderers[i].enabled;

        _explorationCanvases = canvases.ToArray();
        _explorationCanvasesWereEnabled = new bool[_explorationCanvases.Length];
        for (var i = 0; i < _explorationCanvases.Length; i++)
            _explorationCanvasesWereEnabled[i] = _explorationCanvases[i] != null && _explorationCanvases[i].enabled;
    }

    /// <summary>Call from encounter zones, UI, etc.</summary>
    public void BeginCombat()
    {
        if (_loadOp != null)
            return;

        if (string.IsNullOrEmpty(combatSceneName))
        {
            Debug.LogError($"{nameof(CombatAdditiveCoordinator)}: combat scene name is empty.", this);
            return;
        }

        // Refresh so we never use Camera.main after combat exists; exploration-only refs only.
        CacheExplorationSceneOutput();

        _loadOp = SceneManager.LoadSceneAsync(combatSceneName, LoadSceneMode.Additive);
        if (_loadOp == null)
        {
            Debug.LogError(
                $"{nameof(CombatAdditiveCoordinator)}: Could not load '{combatSceneName}'. Add the scene to Build Settings and use the exact scene name (see the .unity file name).",
                this);
            return;
        }

        _loadOp.completed += _ =>
        {
            _loadOp = null;
            var loaded = SceneManager.GetSceneByName(combatSceneName);
            if (!loaded.IsValid() || !loaded.isLoaded)
            {
                Debug.LogError(
                    $"{nameof(CombatAdditiveCoordinator)}: Scene '{combatSceneName}' did not load. Check spelling — it must match the name in File > Build Settings.",
                    this);
            }
        };
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != combatSceneName)
            return;

        SetExplorationOutputEnabled(false);

        if (pauseExplorationWithTimeScale)
        {
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }
    }

    private void OnSceneUnloaded(Scene scene)
    {
        if (scene.name != combatSceneName)
            return;

        SetExplorationOutputEnabled(true);

        if (pauseExplorationWithTimeScale)
            Time.timeScale = _savedTimeScale;

        if (CombatSession.PeekVictoryLootPending())
        {
            CombatSession.ClearVictoryLootPending();
            TryGrantVictoryCombatLoot();
        }

        CombatSession.RaiseCombatEnded();
        CombatSession.Clear();
    }

    private void TryGrantVictoryCombatLoot()
    {
        var pool = BuildDropPool();
        if (pool.Count == 0)
        {
            Debug.LogWarning($"{nameof(CombatAdditiveCoordinator)}: No items in {nameof(combatDropPool)} — skipping victory loot.", this);
            return;
        }

        var inv = playerInventory != null ? playerInventory : FindAnyObjectByType<Inventory>();
        if (inv == null)
        {
            Debug.LogWarning($"{nameof(CombatAdditiveCoordinator)}: No {nameof(Inventory)} found — cannot grant victory loot.", this);
            return;
        }

        var lo = Mathf.Max(1, minDropsOnVictory);
        var hi = Mathf.Max(lo, maxDropsOnVictory);
        var planned = UnityEngine.Random.Range(lo, hi + 1);
        var added = 0;

        for (var i = 0; i < planned; i++)
        {
            var item = pool[UnityEngine.Random.Range(0, pool.Count)];
            var overflow = inv.TryAdd(item, 1);
            if (overflow > 0)
            {
                Debug.LogWarning(
                    $"{nameof(CombatAdditiveCoordinator)}: Inventory full — could not add all victory loot ({item.DisplayName} lost).",
                    this);
                break;
            }

            added++;
        }

        Debug.Log($"[Combat] Victory loot: added {added}/{planned} random drop(s) (pool has {pool.Count} item type(s)).", this);
    }

    private List<ItemDefinition> BuildDropPool()
    {
        var list = new List<ItemDefinition>();
        if (combatDropPool != null)
        {
            foreach (var def in combatDropPool)
            {
                if (def != null)
                    list.Add(def);
            }
        }

        if (list.Count > 0)
            return list;

        var fallback = Resources.Load<CombatVictoryDropPool>("Combat/VictoryDropPool");
        if (fallback?.Items != null)
        {
            foreach (var def in fallback.Items)
            {
                if (def != null)
                    list.Add(def);
            }
        }

        return list;
    }

    private void SetExplorationOutputEnabled(bool enabled)
    {
        if (_explorationCameras != null)
        {
            for (var i = 0; i < _explorationCameras.Length; i++)
            {
                var cam = _explorationCameras[i];
                if (cam == null)
                    continue;

                if (enabled)
                    cam.enabled = _explorationCamerasWereEnabled[i];
                else
                    cam.enabled = false;
            }
        }

        if (_explorationListeners != null)
        {
            for (var i = 0; i < _explorationListeners.Length; i++)
            {
                var listener = _explorationListeners[i];
                if (listener == null)
                    continue;

                if (enabled)
                    listener.enabled = _explorationListenersWereEnabled[i];
                else
                    listener.enabled = false;
            }
        }

        if (_explorationRenderers != null)
        {
            for (var i = 0; i < _explorationRenderers.Length; i++)
            {
                var r = _explorationRenderers[i];
                if (r == null)
                    continue;

                if (enabled)
                    r.enabled = _explorationRenderersWereEnabled[i];
                else
                    r.enabled = false;
            }
        }

        if (_explorationCanvases != null)
        {
            for (var i = 0; i < _explorationCanvases.Length; i++)
            {
                var c = _explorationCanvases[i];
                if (c == null)
                    continue;

                if (enabled)
                    c.enabled = _explorationCanvasesWereEnabled[i];
                else
                    c.enabled = false;
            }
        }
    }
}
