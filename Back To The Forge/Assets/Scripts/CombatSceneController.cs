using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Place on a root object in the combat scene. Call <see cref="EndCombat"/> when the battle is over.
/// </summary>
public class CombatSceneController : MonoBehaviour
{
    [SerializeField] private string combatSceneName = "Combat Scene";

    private bool _unloading;

    /// <summary>Unloads this combat scene additively so exploration resumes.</summary>
    public void EndCombat()
    {
        if (_unloading)
            return;

        StartCoroutine(UnloadRoutine());
    }

    private IEnumerator UnloadRoutine()
    {
        _unloading = true;

        var op = SceneManager.UnloadSceneAsync(combatSceneName);
        if (op == null)
        {
            Debug.LogError($"{nameof(CombatSceneController)}: could not unload '{combatSceneName}'. Is the name correct and in Build Settings?", this);
            _unloading = false;
            yield break;
        }

        yield return op;

        // CombatSession.Clear() is handled by CombatAdditiveCoordinator.OnSceneUnloaded after victory loot.
        _unloading = false;
    }
}
