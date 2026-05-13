using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World-space bar under a <see cref="CombatUnit"/>. Toggle <see cref="showOnlyForEnemies"/> to hide on allies.
/// </summary>
public class CombatUnitHealthBar : MonoBehaviour
{
    [SerializeField] private bool showOnlyForEnemies;
    [SerializeField] private Image fillImage;
    [SerializeField] private TextMeshProUGUI hpText;

    [Header("Fill colour by HP % (high → low)")]
    [SerializeField] private Color fullHealthColor = new(0.25f, 0.92f, 0.38f, 1f);
    [SerializeField] private Color midHealthColor = new(0.95f, 0.18f, 0.15f, 1f);
    [SerializeField] private Color emptyHealthColor = new(0.04f, 0.04f, 0.04f, 1f);

    private CombatUnit _unit;
    private bool _hpLabelCreated;

    private void Awake()
    {
        _unit = GetComponentInParent<CombatUnit>();
        EnsureHpLabel();
    }

    private void OnEnable()
    {
        if (_unit == null)
            _unit = GetComponentInParent<CombatUnit>();

        if (_unit != null)
        {
            _unit.HpChanged += OnHpChanged;
            EnsureHpLabel();
            Refresh(_unit.CurrentHp, _unit.MaxHp);
        }
    }

    private void OnDisable()
    {
        if (_unit != null)
            _unit.HpChanged -= OnHpChanged;
    }

    private void OnHpChanged(int current, int max)
    {
        Refresh(current, max);
    }

    private void Refresh(int current, int max)
    {
        if (_unit == null)
            _unit = GetComponentInParent<CombatUnit>();

        EnsureHpLabel();

        if (showOnlyForEnemies && _unit != null && _unit.IsAlly)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        if (fillImage == null)
            return;

        var t = max > 0 ? (float)current / max : 0f;
        t = Mathf.Clamp01(t);
        fillImage.fillAmount = t;
        fillImage.color = HealthToColor(t);

        if (hpText != null)
            hpText.text = max > 0 ? $"{current}/{max}" : string.Empty;
    }

    private void EnsureHpLabel()
    {
        if (hpText != null || fillImage == null || _hpLabelCreated)
            return;

        var canvas = fillImage.GetComponentInParent<Canvas>();
        if (canvas == null)
            return;

        canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1 |
                                            AdditionalCanvasShaderChannels.TexCoord2 |
                                            AdditionalCanvasShaderChannels.Normal |
                                            AdditionalCanvasShaderChannels.Tangent;

        var go = new GameObject("HpText", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 14f);
        rt.sizeDelta = new Vector2(140f, 22f);

        hpText = go.AddComponent<TextMeshProUGUI>();
        hpText.fontSize = 11;
        hpText.fontStyle = FontStyles.Bold;
        hpText.alignment = TextAlignmentOptions.Center;
        hpText.textWrappingMode = TextWrappingModes.NoWrap;
        hpText.color = new Color(1f, 1f, 1f, 0.95f);
        hpText.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
        {
            hpText.font = TMP_Settings.defaultFontAsset;
            hpText.fontSharedMaterial = TMP_Settings.defaultFontAsset.material;
        }

        _hpLabelCreated = true;
    }

    /// <summary>100%–50%: green → red. 50%–0%: red → black.</summary>
    private Color HealthToColor(float hp01)
    {
        if (hp01 >= 0.5f)
        {
            var u = (hp01 - 0.5f) / 0.5f;
            return Color.Lerp(midHealthColor, fullHealthColor, u);
        }

        var v = hp01 / 0.5f;
        return Color.Lerp(emptyHealthColor, midHealthColor, v);
    }
}
