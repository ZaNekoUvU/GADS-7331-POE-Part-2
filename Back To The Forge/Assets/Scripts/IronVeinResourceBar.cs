using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World-space bar above an <see cref="IronVein"/> showing remaining ore. Matches HUD blue styling.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(IronVein))]
public sealed class IronVeinResourceBar : MonoBehaviour
{
    private static Sprite _whiteSprite;

    [SerializeField] private Vector2 barSize = new(1.4f, 0.18f);
    [SerializeField] private float heightAboveBounds = 0.25f;

    private IronVein _vein;
    private Canvas _canvas;
    private RectTransform _barRoot;
    private Image _fillImage;

    private void Awake()
    {
        _vein = GetComponent<IronVein>();
        BuildBar();
    }

    private void LateUpdate()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (_canvas == null || _vein == null)
            return;

        if (!_vein.HasOreLeft || !ShouldShow())
        {
            _canvas.enabled = false;
            return;
        }

        _canvas.enabled = true;
        PositionBar();

        var fill = _vein.GetDisplayedRemainingFraction(
            PlayerMiningController.TryGetActiveMiningState(_vein, out var tick01),
            tick01);

        if (_fillImage != null)
            _fillImage.fillAmount = fill;
    }

    private bool ShouldShow()
    {
        var player = PlayerMovement2D.Instance ?? FindAnyObjectByType<PlayerMovement2D>();
        if (player == null)
            return false;

        if (PlayerMiningController.TryGetActiveMiningState(_vein, out _))
            return true;

        return _vein.IsInGatherRange(player.transform.position);
    }

    private void PositionBar()
    {
        var bounds = GetVisualBounds();
        var center = bounds.center;
        center.y = bounds.max.y + heightAboveBounds;

        if (_barRoot != null)
            _barRoot.position = center;
    }

    private Bounds GetVisualBounds()
    {
        var renderers = GetComponentsInChildren<SpriteRenderer>();
        if (renderers.Length == 0)
            return new Bounds(transform.position, Vector3.one);

        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private void BuildBar()
    {
        var root = new GameObject("ResourceBar", typeof(RectTransform));
        root.transform.SetParent(transform, false);

        _canvas = root.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.sortingOrder = 120;

        root.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 100f;

        _barRoot = root.GetComponent<RectTransform>();
        _barRoot.sizeDelta = barSize * 100f;
        _barRoot.localScale = Vector3.one * 0.01f;

        var border = CreateBarImage(root.transform, "Border", Color.white);
        border.rectTransform.anchorMin = Vector2.zero;
        border.rectTransform.anchorMax = Vector2.one;
        border.rectTransform.offsetMin = Vector2.zero;
        border.rectTransform.offsetMax = Vector2.zero;

        var background = CreateBarImage(root.transform, "Background", new Color(0.08f, 0.08f, 0.12f, 0.92f));
        var backgroundRt = background.rectTransform;
        backgroundRt.anchorMin = Vector2.zero;
        backgroundRt.anchorMax = Vector2.one;
        backgroundRt.offsetMin = new Vector2(2f, 2f);
        backgroundRt.offsetMax = new Vector2(-2f, -2f);

        var fillHost = new GameObject("FillHost", typeof(RectTransform));
        fillHost.transform.SetParent(root.transform, false);
        var fillHostRt = fillHost.GetComponent<RectTransform>();
        fillHostRt.anchorMin = Vector2.zero;
        fillHostRt.anchorMax = Vector2.one;
        fillHostRt.offsetMin = new Vector2(2f, 2f);
        fillHostRt.offsetMax = new Vector2(-2f, -2f);

        _fillImage = CreateBarImage(fillHost.transform, "Fill", FfStyleMenuUi.HudPanelBlue);
        _fillImage.type = Image.Type.Filled;
        _fillImage.fillMethod = Image.FillMethod.Horizontal;
        _fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        _fillImage.fillAmount = 1f;
    }

    private static Image CreateBarImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var image = go.AddComponent<Image>();
        image.sprite = GetWhiteSprite();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Sprite GetWhiteSprite()
    {
        if (_whiteSprite != null)
            return _whiteSprite;

        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        tex.Apply();

        _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
        return _whiteSprite;
    }
}
