using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Lives in the exploration scene. Loads combat additively, hides everything outside the combat scene
/// (cameras, listeners, renderers, canvases) so the combat camera only draws the combat background + units.
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
    private bool _explorationPausedForCombat;

    private List<(Camera cam, bool wasEnabled)> _cameraIsolationRestore;
    private List<(AudioListener listener, bool wasEnabled)> _listenerIsolationRestore;
    private List<(Renderer renderer, bool wasEnabled)> _rendererIsolationRestore;
    private List<(Canvas canvas, bool wasEnabled)> _canvasIsolationRestore;

    /// <summary>True while combat is loading, active, or the pre-fight intro has paused exploration.</summary>
    public bool IsCombatActiveOrLoading
    {
        get
        {
            if (_explorationPausedForCombat || _loadOp != null)
                return true;

            if (string.IsNullOrEmpty(combatSceneName))
                return false;

            var scene = SceneManager.GetSceneByName(combatSceneName);
            return scene.IsValid() && scene.isLoaded;
        }
    }

    /// <summary>Freezes exploration scaled time so risky ground and movement cannot chain another fight.</summary>
    public void PauseExplorationForCombat()
    {
        if (!_explorationPausedForCombat)
        {
            _explorationPausedForCombat = true;
            _savedTimeScale = Time.timeScale;
            if (_savedTimeScale <= 0.01f)
                _savedTimeScale = 1f;
        }

        Time.timeScale = 0f;
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

    /// <summary>Obsolete: isolation no longer uses a per-exploration cache. Kept for API compatibility.</summary>
    public void RefreshExplorationOutputCache()
    {
        // No-op — combat isolation walks all loaded objects by scene.
    }

    /// <summary>Call from encounter zones, UI, etc.</summary>
    public void BeginCombat()
    {
        if (_loadOp != null)
            return;

        PauseExplorationForCombat();

        if (string.IsNullOrEmpty(combatSceneName))
        {
            Debug.LogError($"{nameof(CombatAdditiveCoordinator)}: combat scene name is empty.", this);
            return;
        }

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

        ApplyCombatSceneIsolation();

        if (pauseExplorationWithTimeScale)
            PauseExplorationForCombat();
    }

    private void OnSceneUnloaded(Scene scene)
    {
        if (scene.name != combatSceneName)
            return;

        RestoreCombatSceneIsolation();

        if (pauseExplorationWithTimeScale)
        {
            _explorationPausedForCombat = false;
            Time.timeScale = _savedTimeScale > 0.01f ? _savedTimeScale : 1f;
        }

        if (CombatSession.PeekVictoryLootPending())
        {
            CombatSession.ClearVictoryLootPending();
            TryGrantVictoryCombatLoot();
        }

        CombatSession.RaiseCombatEnded();
        CombatSession.Clear();
    }

    /// <summary>
    /// Only the combat scene may render or hear audio; exploration / DDOL world meshes are hidden so the combat camera
    /// cannot accidentally composite exploration tiles behind combat sprites.
    /// </summary>
    private void ApplyCombatSceneIsolation()
    {
        RestoreCombatSceneIsolation();

        var combatScene = SceneManager.GetSceneByName(combatSceneName);
        if (!combatScene.IsValid())
            return;

        _cameraIsolationRestore = new List<(Camera, bool)>();
        foreach (var cam in FindObjectsByType<Camera>(FindObjectsInactive.Include))
        {
            if (cam == null || !cam.gameObject.scene.IsValid())
                continue;

            var inCombat = cam.gameObject.scene == combatScene;
            _cameraIsolationRestore.Add((cam, cam.enabled));
            cam.enabled = inCombat;
        }

        _listenerIsolationRestore = new List<(AudioListener, bool)>();
        foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsInactive.Include))
        {
            if (listener == null || !listener.gameObject.scene.IsValid())
                continue;

            var inCombat = listener.gameObject.scene == combatScene;
            _listenerIsolationRestore.Add((listener, listener.enabled));
            listener.enabled = inCombat;
        }

        _rendererIsolationRestore = new List<(Renderer, bool)>();
        foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Include))
        {
            if (renderer == null || !renderer.gameObject.scene.IsValid())
                continue;

            var inCombat = renderer.gameObject.scene == combatScene;
            _rendererIsolationRestore.Add((renderer, renderer.enabled));
            renderer.enabled = inCombat;
        }

        _canvasIsolationRestore = new List<(Canvas, bool)>();
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include))
        {
            if (canvas == null || !canvas.gameObject.scene.IsValid())
                continue;

            var inCombat = canvas.gameObject.scene == combatScene;
            _canvasIsolationRestore.Add((canvas, canvas.enabled));
            canvas.enabled = inCombat;
        }
    }

    private void RestoreCombatSceneIsolation()
    {
        if (_cameraIsolationRestore != null)
        {
            foreach (var (cam, was) in _cameraIsolationRestore)
            {
                if (cam != null)
                    cam.enabled = was;
            }

            _cameraIsolationRestore = null;
        }

        if (_listenerIsolationRestore != null)
        {
            foreach (var (listener, was) in _listenerIsolationRestore)
            {
                if (listener != null)
                    listener.enabled = was;
            }

            _listenerIsolationRestore = null;
        }

        if (_rendererIsolationRestore != null)
        {
            foreach (var (renderer, was) in _rendererIsolationRestore)
            {
                if (renderer != null)
                    renderer.enabled = was;
            }

            _rendererIsolationRestore = null;
        }

        if (_canvasIsolationRestore != null)
        {
            foreach (var (canvas, was) in _canvasIsolationRestore)
            {
                if (canvas != null)
                    canvas.enabled = was;
            }

            _canvasIsolationRestore = null;
        }
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
}
