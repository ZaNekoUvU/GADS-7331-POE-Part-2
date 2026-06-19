using UnityEngine;

/// <summary>
/// Clip references for <see cref="GameAudioController"/>. Loaded from Resources at runtime.
/// </summary>
[CreateAssetMenu(fileName = "GameAudioLibrary", menuName = "Audio/Game Audio Library")]
public class GameAudioLibrary : ScriptableObject
{
    [Header("Music")]
    public AudioClip mainMenuMusic;
    public AudioClip explorationMusic;
    public AudioClip combatMusic;

    [Header("Quest")]
    public AudioClip questCompleteSfx;
    public AudioClip questItemObtainedSfx;

    [Header("Combat — engage")]
    public AudioClip goblinEngageSfx;
    public AudioClip ogreEngageSfx;
    public AudioClip slimeEngageSfx;

    [Header("Combat — death")]
    public AudioClip playerAlliesDeathSfx;
    public AudioClip goblinDeathSfx;
    public AudioClip slimeDeathSfx;
    public AudioClip ogreDeathSfx;

    [Header("Combat — actions")]
    public AudioClip attackSfx;
}
