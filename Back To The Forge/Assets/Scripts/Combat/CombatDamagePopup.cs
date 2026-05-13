using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Brief world-space number shown above a combatant when they take damage.
/// </summary>
public sealed class CombatDamagePopup : MonoBehaviour
{
    [SerializeField] private float riseWorldUnits = 0.45f;
    [SerializeField] private float durationSeconds = 0.85f;

    private TextMeshProUGUI _tmp;
    private RectTransform _rt;
    private CanvasGroup _group;
    private Vector3 _start;

    public static void SpawnAt(Vector3 worldPosition, int damage, bool victimIsAlly)
    {
        if (damage <= 0)
            return;

        var go = new GameObject("DamagePopup");
        go.transform.position = worldPosition;
        var host = go.AddComponent<CombatDamagePopup>();
        host.Build(damage, victimIsAlly);
    }

    private void Build(int damage, bool victimIsAlly)
    {
        _start = transform.position;

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 40;

        gameObject.AddComponent<CanvasRenderer>();
        _group = gameObject.AddComponent<CanvasGroup>();
        _group.blocksRaycasts = false;
        _group.interactable = false;

        _rt = gameObject.GetComponent<RectTransform>();
        _rt.sizeDelta = new Vector2(2f, 0.6f);
        _rt.localScale = new Vector3(0.015f, 0.015f, 0.015f);

        _tmp = gameObject.AddComponent<TextMeshProUGUI>();
        _tmp.text = damage.ToString();
        _tmp.fontSize = 42;
        _tmp.fontStyle = FontStyles.Bold;
        _tmp.alignment = TextAlignmentOptions.Center;
        _tmp.textWrappingMode = TextWrappingModes.NoWrap;
        _tmp.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
        {
            _tmp.font = TMP_Settings.defaultFontAsset;
            _tmp.fontSharedMaterial = TMP_Settings.defaultFontAsset.material;
        }

        _tmp.color = victimIsAlly
            ? new Color(1f, 0.45f, 0.2f, 1f)
            : new Color(1f, 0.92f, 0.35f, 1f);

        StartCoroutine(Animate());
    }

    private IEnumerator Animate()
    {
        var t = 0f;
        while (t < durationSeconds)
        {
            t += Time.unscaledDeltaTime;
            var u = Mathf.Clamp01(t / durationSeconds);
            transform.position = _start + Vector3.up * (riseWorldUnits * u);
            if (_group != null)
                _group.alpha = 1f - u * u;
            yield return null;
        }

        Destroy(gameObject);
    }
}
