using UnityEngine;

/// <summary>
/// Red pixel-outline on a combat unit sprite while it is the selected strike target.
/// </summary>
[DisallowMultipleComponent]
public class CombatTargetSpriteHighlight : MonoBehaviour
{
    private static readonly Vector2[] OutlineOffsets =
    {
        new(0.055f, 0f),
        new(-0.055f, 0f),
        new(0f, 0.055f),
        new(0f, -0.055f),
        new(0.039f, 0.039f),
        new(-0.039f, 0.039f),
        new(0.039f, -0.039f),
        new(-0.039f, -0.039f)
    };

    [SerializeField] private Color outlineColor = new(1f, 0.18f, 0.18f, 1f);

    private SpriteRenderer _source;
    private GameObject _root;
    private SpriteRenderer[] _layers;
    private bool _highlighted;

    private void Awake()
    {
        var unit = GetComponent<CombatUnit>();
        _source = unit != null ? unit.SpriteRenderer : GetComponent<SpriteRenderer>();
    }

    private void LateUpdate()
    {
        if (!_highlighted || _source == null || _layers == null)
            return;

        SyncFromSource();
    }

    private void OnDisable()
    {
        SetHighlighted(false);
    }

    public void SetHighlighted(bool on)
    {
        if (on == _highlighted && (!on || _root != null))
            return;

        _highlighted = on;
        EnsureBuilt();

        if (_root == null)
            return;

        if (on)
            SyncFromSource();

        _root.SetActive(on);
    }

    private void EnsureBuilt()
    {
        if (_root != null || _source == null)
            return;

        _root = new GameObject("TargetOutline");
        _root.transform.SetParent(_source.transform, false);

        _layers = new SpriteRenderer[OutlineOffsets.Length];
        for (var i = 0; i < OutlineOffsets.Length; i++)
        {
            var layerGo = new GameObject($"layer{i}");
            layerGo.transform.SetParent(_root.transform, false);
            layerGo.transform.localPosition = new Vector3(OutlineOffsets[i].x, OutlineOffsets[i].y, 0f);

            var layer = layerGo.AddComponent<SpriteRenderer>();
            layer.sortingLayerID = _source.sortingLayerID;
            layer.sortingOrder = _source.sortingOrder - 1;
            layer.color = outlineColor;
            _layers[i] = layer;
        }

        _root.SetActive(false);
    }

    private void SyncFromSource()
    {
        if (_source == null || _layers == null)
            return;

        var sprite = _source.sprite;
        var layerId = _source.sortingLayerID;
        var order = _source.sortingOrder - 1;

        for (var i = 0; i < _layers.Length; i++)
        {
            var layer = _layers[i];
            if (layer == null)
                continue;

            layer.sprite = sprite;
            layer.flipX = _source.flipX;
            layer.flipY = _source.flipY;
            layer.sortingLayerID = layerId;
            layer.sortingOrder = order;
            layer.color = outlineColor;
        }
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
