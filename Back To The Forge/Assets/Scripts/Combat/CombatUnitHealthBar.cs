using UnityEngine;
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

        if (fillImage == null)
            return;

        var t = max > 0 ? (float)current / max : 0f;
        t = Mathf.Clamp01(t);
        fillImage.fillAmount = t;
        fillImage.color = HealthToColor(t);
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
