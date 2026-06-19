using UnityEngine;

/// <summary>
/// Crisp white border on a combat unit sprite while it is the selected strike target.
/// </summary>
[DisallowMultipleComponent]
public class CombatTargetSpriteHighlight : MonoBehaviour
{
    private static Material _outlineTemplate;

    [SerializeField] private Material outlineMaterial;
    [SerializeField] private Color borderColor = Color.white;
    [SerializeField] private float borderPixelWidth = 2f;

    private SpriteRenderer _source;
    private Material _defaultMaterial;
    private Material _activeOutlineMaterial;
    private bool _highlighted;

    private void Awake()
    {
        var unit = GetComponent<CombatUnit>();
        _source = unit != null ? unit.SpriteRenderer : GetComponent<SpriteRenderer>();

        if (_source != null)
            _defaultMaterial = _source.sharedMaterial;
    }

    private void OnDisable()
    {
        SetHighlighted(false);
    }

    private void OnDestroy()
    {
        if (_activeOutlineMaterial != null)
            Destroy(_activeOutlineMaterial);
    }

    public void SetHighlighted(bool on)
    {
        if (_source == null)
            return;

        if (on == _highlighted)
            return;

        _highlighted = on;

        if (on)
        {
            var outline = GetOutlineMaterialInstance();
            if (outline == null)
                return;

            outline.SetColor("_OutlineColor", borderColor);
            outline.SetFloat("_OutlinePixelWidth", borderPixelWidth);
            _source.material = outline;
        }
        else
        {
            _source.sharedMaterial = _defaultMaterial;
        }
    }

    private Material GetOutlineMaterialInstance()
    {
        if (_activeOutlineMaterial != null)
            return _activeOutlineMaterial;

        var template = outlineMaterial != null ? outlineMaterial : GetOutlineTemplate();
        if (template == null)
            return null;

        _activeOutlineMaterial = new Material(template);
        return _activeOutlineMaterial;
    }

    private static Material GetOutlineTemplate()
    {
        if (_outlineTemplate != null)
            return _outlineTemplate;

        var shader = Shader.Find("Custom/SpriteWhiteOutline");
        if (shader == null)
        {
            Debug.LogWarning($"{nameof(CombatTargetSpriteHighlight)}: Outline shader not found.");
            return null;
        }

        _outlineTemplate = new Material(shader);
        return _outlineTemplate;
    }

    public static CombatTargetSpriteHighlight GetOrAdd(CombatUnit unit)
    {
        if (unit == null)
            return null;

        var highlight = unit.GetComponent<CombatTargetSpriteHighlight>();
        if (highlight == null)
            highlight = unit.gameObject.AddComponent<CombatTargetSpriteHighlight>();

        return highlight;
    }
}
