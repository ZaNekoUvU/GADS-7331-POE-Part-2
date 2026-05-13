using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// World-space bar under a <see cref="CombatUnit"/>. Toggle <see cref="showOnlyForEnemies"/> to hide on allies.
/// </summary>
public class CombatUnitHealthBar : MonoBehaviour
{
    [SerializeField] private bool showOnlyForEnemies;
    [SerializeField] private Image fillImage;

    [Header("Fill colour by HP % (high → low)")]
    [SerializeField] private Color fullHealthColor = new(0.25f, 0.92f, 0.38f, 1f);
    [SerializeField] private Color midHealthColor = new(0.95f, 0.18f, 0.15f, 1f);
    [SerializeField] private Color emptyHealthColor = new(0.04f, 0.04f, 0.04f, 1f);

    private CombatUnit _unit;
    private TextMeshProUGUI _hpTmp;
    private bool _hpLabelCreated;

    private void Awake()
    {
        _unit = GetComponentInParent<CombatUnit>();
    }

    private void OnEnable()
    {
        if (_unit == null)
            _unit = GetComponentInParent<CombatUnit>();

        if (_unit != null)
        {
            _unit.HpChanged += OnHpChanged;
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

        if (showOnlyForEnemies && _unit != null && _unit.IsAlly)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        if (fillImage != null)
        {
            var t = max > 0 ? (float)current / max : 0f;
            t = Mathf.Clamp01(t);
            fillImage.fillAmount = t;
            fillImage.color = HealthToColor(t);
        }

        EnsureHpLabel();
        if (_hpTmp != null)
            _hpTmp.text = max > 0 ? $"{current}/{max}" : string.Empty;
    }

    /// <summary>TMP on the same world canvas as the bar (shared scale) so numbers fit inside the strip.</summary>
    private void EnsureHpLabel()
    {
        if (_hpTmp != null || fillImage == null || _hpLabelCreated)
            return;

        var barCanvas = fillImage.canvas;
        if (barCanvas == null)
            return;

        barCanvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;

        var host = new GameObject("HpText", typeof(RectTransform));
        host.transform.SetParent(barCanvas.transform, false);
        host.transform.SetAsLastSibling();

        var rt = host.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(2f, 1f);
        rt.offsetMax = new Vector2(-2f, -1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;

        _hpTmp = host.AddComponent<TextMeshProUGUI>();
        _hpTmp.raycastTarget = false;
        _hpTmp.textWrappingMode = TextWrappingModes.NoWrap;
        _hpTmp.fontSize = 9f;
        _hpTmp.fontStyle = FontStyles.Bold;
        _hpTmp.alignment = TextAlignmentOptions.Center;
        _hpTmp.margin = Vector4.zero;
        _hpTmp.color = new Color(1f, 1f, 1f, 0.95f);
        if (TMP_Settings.defaultFontAsset != null)
        {
            _hpTmp.font = TMP_Settings.defaultFontAsset;
            _hpTmp.fontSharedMaterial = TMP_Settings.defaultFontAsset.material;
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
