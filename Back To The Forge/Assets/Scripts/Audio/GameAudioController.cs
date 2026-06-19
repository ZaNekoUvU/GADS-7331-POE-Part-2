using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// DontDestroyOnLoad music and SFX. Uses the scene/player AudioListener — does not add its own.
/// </summary>
[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
public class GameAudioController : MonoBehaviour
{
    public const string MainMenuSceneName = "Main Menu";
    public const string CombatSceneName = "Combat Scene";

    public static GameAudioController Instance { get; private set; }

    [SerializeField] private float musicVolume = 0.55f;
    [SerializeField] private float sfxVolume = 0.85f;
    [Tooltip("Music volume multiplier while NPC or combat-intro dialogue is open.")]
    [SerializeField] [Range(0.05f, 1f)] private float dialogueMusicMultiplier = 0.22f;
    [Tooltip("How quickly music volume fades in/out during dialogue (per second).")]
    [SerializeField] private float musicVolumeFadeSpeed = 2.5f;

    private AudioSource _musicSource;
    private AudioSource _sfxSource;

    private AudioClip _mainMenuMusic;
    private AudioClip _explorationMusic;
    private AudioClip _combatMusic;
    private AudioClip _questCompleteSfx;
    private AudioClip _questItemObtainedSfx;
    private AudioClip _goblinEngageSfx;
    private AudioClip _ogreEngageSfx;
    private AudioClip _slimeEngageSfx;
    private AudioClip _playerAlliesDeathSfx;
    private AudioClip _goblinDeathSfx;
    private AudioClip _slimeDeathSfx;
    private AudioClip _ogreDeathSfx;
    private AudioClip _attackSfx;

    private bool _combatSceneLoaded;
    private bool _engagePlayedThisCombat;
    private AudioClip _explorationMusicClip;
    private bool _clipsLoaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreateIfMissing()
    {
        if (Instance != null || FindAnyObjectByType<GameAudioController>() != null)
            return;

        var go = new GameObject($"[{nameof(GameAudioController)}]");
        go.AddComponent<GameAudioController>();
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

        _musicSource = gameObject.AddComponent<AudioSource>();
        _musicSource.playOnAwake = false;
        _musicSource.loop = true;
        _musicSource.volume = musicVolume;
        _musicSource.spatialBlend = 0f;
        _musicSource.ignoreListenerPause = true;

        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.playOnAwake = false;
        _sfxSource.loop = false;
        _sfxSource.volume = sfxVolume;
        _sfxSource.spatialBlend = 0f;
        _sfxSource.ignoreListenerPause = true;

        LoadAllClips();
        TryPlayMusicForActiveScene();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        CombatSession.CombatEnded += OnCombatEnded;
        CombatUnitSpawner.EnemiesSpawned += OnEnemiesSpawned;
        CombatUnit.OnDefeated += OnEnemyDefeated;
        CombatUnit.OnAllyDefeated += OnAllyDefeated;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        CombatSession.CombatEnded -= OnCombatEnded;
        CombatUnitSpawner.EnemiesSpawned -= OnEnemiesSpawned;
        CombatUnit.OnDefeated -= OnEnemyDefeated;
        CombatUnit.OnAllyDefeated -= OnAllyDefeated;
    }

    private void Start()
    {
        StartCoroutine(BootstrapMusicRoutine());
    }

    private void Update()
    {
        UpdateMusicVolumeFade();
    }

    private void UpdateMusicVolumeFade()
    {
        if (_musicSource == null)
            return;

        var target = GetTargetMusicVolume();
        if (Mathf.Approximately(_musicSource.volume, target))
            return;

        _musicSource.volume = Mathf.MoveTowards(
            _musicSource.volume,
            target,
            musicVolumeFadeSpeed * Time.unscaledDeltaTime);
    }

    private float GetTargetMusicVolume()
    {
        return ShouldDuckMusicForDialogue() ? musicVolume * dialogueMusicMultiplier : musicVolume;
    }

    private static bool ShouldDuckMusicForDialogue()
    {
        return SimpleRpgDialogueUI.IsDialogueOpen;
    }

    private IEnumerator BootstrapMusicRoutine()
    {
        for (var i = 0; i < 8; i++)
        {
            yield return null;

            if (!_clipsLoaded)
                LoadAllClips();

            if (HasActiveAudioListener())
            {
                TryPlayMusicForActiveScene();
                if (_musicSource != null && _musicSource.isPlaying)
                    yield break;
            }
        }

        TryPlayMusicForActiveScene();
    }

    /// <summary>Call when a scene finishes booting (e.g. main menu UI ready).</summary>
    public static void RefreshMusicForActiveScene()
    {
        Instance?.TryPlayMusicForActiveScene();
    }

    private void TryPlayMusicForActiveScene()
    {
        if (!_clipsLoaded)
            LoadAllClips();

        ApplySceneMusic(SceneManager.GetActiveScene());
    }

    private static bool HasActiveAudioListener()
    {
        foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsInactive.Include))
        {
            if (listener != null && listener.enabled && listener.gameObject.activeInHierarchy)
                return true;
        }

        return false;
    }

    private void LoadAllClips()
    {
        var library = Resources.Load<GameAudioLibrary>("GameAudioLibrary");
        var audioResources = LoadAllAudioResources();

        _mainMenuMusic = ResolveClip(audioResources, library?.mainMenuMusic, "Main Menu");
        _explorationMusic = ResolveClip(audioResources, library?.explorationMusic, "Exploration Music");
        _combatMusic = ResolveClip(audioResources, library?.combatMusic, "Combat Music");
        _questCompleteSfx = ResolveClip(audioResources, library?.questCompleteSfx, "Quest Complete sfx");
        _questItemObtainedSfx = ResolveClip(audioResources, library?.questItemObtainedSfx, "Quest Item Obtained sfx");
        _goblinEngageSfx = ResolveClip(audioResources, library?.goblinEngageSfx, "Goblin Engage sfx");
        _ogreEngageSfx = ResolveClip(audioResources, library?.ogreEngageSfx, "Ogre Engage Combat sfx");
        _slimeEngageSfx = library?.slimeEngageSfx;
        _playerAlliesDeathSfx = ResolveClip(audioResources, library?.playerAlliesDeathSfx, "PlayerAllies Death sfx");
        _goblinDeathSfx = ResolveClip(audioResources, library?.goblinDeathSfx, "Goblin Death sfx");
        _slimeDeathSfx = ResolveClip(audioResources, library?.slimeDeathSfx, "Slime Death sfx");
        _ogreDeathSfx = ResolveClip(audioResources, library?.ogreDeathSfx, "Ogre Death sfx");
        _attackSfx = ResolveClip(audioResources, library?.attackSfx, "All Attacks sfx");

        _clipsLoaded = _mainMenuMusic != null || _explorationMusic != null || _combatMusic != null;

        if (_mainMenuMusic == null)
            Debug.LogWarning("[GameAudio] Main menu music clip is missing.", this);

        if (!_clipsLoaded)
            Debug.LogError($"{nameof(GameAudioController)}: Could not load any audio clips from Resources/Audio.", this);
    }

    private static Dictionary<string, AudioClip> LoadAllAudioResources()
    {
        var clips = Resources.LoadAll<AudioClip>("Audio");
        var map = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
        foreach (var clip in clips)
        {
            if (clip != null && !map.ContainsKey(clip.name))
                map.Add(clip.name, clip);
        }

        return map;
    }

    private static AudioClip ResolveClip(
        IReadOnlyDictionary<string, AudioClip> audioResources,
        AudioClip fromLibrary,
        string clipName)
    {
        if (audioResources != null
            && !string.IsNullOrWhiteSpace(clipName)
            && audioResources.TryGetValue(clipName.Trim(), out var fromResources))
            return fromResources;

        if (fromLibrary != null)
            return fromLibrary;

        var pathClip = Resources.Load<AudioClip>($"Audio/{clipName}");
        if (pathClip == null)
            Debug.LogWarning($"[GameAudio] Missing clip '{clipName}' in Resources/Audio.");

        return pathClip;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == CombatSceneName)
        {
            _combatSceneLoaded = true;
            _engagePlayedThisCombat = false;
            PauseExplorationMusic();
            PlayMusic(_combatMusic);

            if (CombatSession.HasRolledWildEncounter)
                TryPlayEngageForEnemyName(CombatSession.ActiveWildEncounter.PickPrimaryDisplayName());

            return;
        }

        if (mode == LoadSceneMode.Single)
            ApplySceneMusic(scene);
    }

    private void OnSceneUnloaded(Scene scene)
    {
        if (scene.name != CombatSceneName)
            return;

        _combatSceneLoaded = false;
        _engagePlayedThisCombat = false;
        StopCombatMusicAndResumeExploration();
    }

    private void OnCombatEnded()
    {
        if (!_combatSceneLoaded)
            StopCombatMusicAndResumeExploration();
    }

    private void ApplySceneMusic(Scene scene)
    {
        if (!scene.IsValid() || _combatSceneLoaded)
            return;

        if (scene.name == MainMenuSceneName)
        {
            _explorationMusicClip = null;
            PlayMusic(_mainMenuMusic);
            return;
        }

        if (scene.name == CombatSceneName)
            return;

        _explorationMusicClip = _explorationMusic;
        PlayMusic(_explorationMusicClip);
    }

    private void PauseExplorationMusic()
    {
        if (_musicSource == null || !_musicSource.isPlaying)
            return;

        _musicSource.Pause();
    }

    private void StopCombatMusicAndResumeExploration()
    {
        if (_musicSource == null)
            return;

        var resumeClip = _explorationMusicClip ?? _explorationMusic;
        if (resumeClip == null)
        {
            _musicSource.Stop();
            return;
        }

        if (_musicSource.clip == resumeClip && _musicSource.time > 0.01f)
        {
            _musicSource.UnPause();
            return;
        }

        PlayMusic(resumeClip);
    }

    private void PlayMusic(AudioClip clip)
    {
        if (_musicSource == null || clip == null)
            return;

        if (_musicSource.clip == clip && _musicSource.isPlaying)
            return;

        if (clip.loadState == AudioDataLoadState.Unloaded)
            clip.LoadAudioData();

        _musicSource.Stop();
        _musicSource.clip = clip;
        _musicSource.loop = true;
        _musicSource.volume = GetTargetMusicVolume();
        _musicSource.Play();
    }

    private void OnEnemiesSpawned(IReadOnlyList<CombatUnit> enemies)
    {
        if (!_combatSceneLoaded || _engagePlayedThisCombat || enemies == null || enemies.Count == 0)
            return;

        for (var i = 0; i < enemies.Count; i++)
        {
            var unit = enemies[i];
            if (unit == null || unit.Definition == null)
                continue;

            if (TryPlayEngageForEnemyName(unit.Definition.DisplayName))
                return;
        }
    }

    private bool TryPlayEngageForEnemyName(string displayName)
    {
        if (_engagePlayedThisCombat || string.IsNullOrWhiteSpace(displayName))
            return false;

        var key = displayName.Trim();
        AudioClip clip = null;

        if (key.IndexOf("bandit", StringComparison.OrdinalIgnoreCase) >= 0)
            return false;

        if (key.IndexOf("goblin", StringComparison.OrdinalIgnoreCase) >= 0)
            clip = _goblinEngageSfx;
        else if (key.IndexOf("ogre", StringComparison.OrdinalIgnoreCase) >= 0)
            clip = _ogreEngageSfx;
        else if (key.IndexOf("slime", StringComparison.OrdinalIgnoreCase) >= 0)
            clip = _slimeEngageSfx;

        if (clip == null)
            return false;

        PlaySfx(clip);
        _engagePlayedThisCombat = true;
        return true;
    }

    private void OnEnemyDefeated(CombatUnit unit)
    {
        if (unit?.Definition == null)
            return;

        PlaySfx(ResolveEnemyDeathClip(unit.Definition.DisplayName));
    }

    private void OnAllyDefeated(CombatUnit unit)
    {
        if (unit == null)
            return;

        PlaySfx(_playerAlliesDeathSfx);
    }

    private AudioClip ResolveEnemyDeathClip(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return null;

        var key = displayName.Trim();
        if (key.IndexOf("bandit", StringComparison.OrdinalIgnoreCase) >= 0)
            return _playerAlliesDeathSfx;
        if (key.IndexOf("goblin", StringComparison.OrdinalIgnoreCase) >= 0)
            return _goblinDeathSfx;
        if (key.IndexOf("slime", StringComparison.OrdinalIgnoreCase) >= 0)
            return _slimeDeathSfx;
        if (key.IndexOf("ogre", StringComparison.OrdinalIgnoreCase) >= 0)
            return _ogreDeathSfx;

        return null;
    }

    public static void PlayQuestComplete()
    {
        Instance?.PlaySfx(Instance._questCompleteSfx);
    }

    public static void PlayQuestItemObtained()
    {
        Instance?.PlaySfx(Instance._questItemObtainedSfx);
    }

    public static void PlayAttackSfx()
    {
        Instance?.PlaySfx(Instance._attackSfx);
    }

    private void PlaySfx(AudioClip clip)
    {
        if (_sfxSource == null || clip == null)
            return;

        _sfxSource.PlayOneShot(clip, sfxVolume);
    }
}
