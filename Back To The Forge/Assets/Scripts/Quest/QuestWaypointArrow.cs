using UnityEngine;

/// <summary>
/// Floating arrow that orbits the player and points toward a world target (blacksmith, quest ore, etc.).
/// </summary>
public class QuestWaypointArrow : MonoBehaviour
{
    [SerializeField] private float orbitRadius = 0.95f;
    [SerializeField] private float bobAmplitude = 0.08f;
    [SerializeField] private float bobSpeed = 2.8f;
    [SerializeField] private float hideWhenWithinDistance = 1.35f;
    [SerializeField] private float visualScale = 0.34f;

    private Transform _follow;
    private SpriteRenderer _sprite;
    private Vector3? _worldTarget;
    private static Sprite _sharedArrowSprite;
    private const int ArrowSpriteRevision = 5;
    private static int _sharedArrowRevision;

    public void SetFollow(Transform follow)
    {
        _follow = follow;
    }

    public void SetWorldTarget(Vector3? worldTarget)
    {
        _worldTarget = worldTarget;
        gameObject.SetActive(worldTarget.HasValue && _follow != null);
    }

    private void Awake()
    {
        _sprite = GetComponent<SpriteRenderer>();
        if (_sprite == null)
            _sprite = gameObject.AddComponent<SpriteRenderer>();

        if (_sharedArrowSprite == null || _sharedArrowRevision != ArrowSpriteRevision)
        {
            _sharedArrowSprite = CreateArrowSprite();
            _sharedArrowRevision = ArrowSpriteRevision;
        }

        _sprite.sprite = _sharedArrowSprite;
        _sprite.color = Color.white;
        _sprite.sortingOrder = 50;
        transform.localScale = Vector3.one * visualScale;
    }

    private void LateUpdate()
    {
        if (_follow == null || !_worldTarget.HasValue)
        {
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
            return;
        }

        var playerPos = _follow.position;
        var target = _worldTarget.Value;
        var delta = target - playerPos;
        delta.z = 0f;

        if (delta.sqrMagnitude < hideWhenWithinDistance * hideWhenWithinDistance)
        {
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
            return;
        }

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        var dir = ((Vector2)delta).normalized;
        var bob = Mathf.Sin(Time.unscaledTime * bobSpeed) * bobAmplitude;
        var offset = dir * orbitRadius;
        transform.position = playerPos + new Vector3(offset.x, offset.y + bob, playerPos.z - 0.5f);

        var angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
    }

    private static Sprite CreateArrowSprite()
    {
        const int w = 12;
        const int h = 18;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        var clear = new Color(0f, 0f, 0f, 0f);
        var fill = FfStyleMenuUi.HudPanelBlue;
        var border = Color.white;

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                if (!IsInsideArrow(x, y, w, h))
                {
                    tex.SetPixel(x, y, clear);
                    continue;
                }

                tex.SetPixel(x, y, IsArrowBorderPixel(x, y, w, h) ? border : fill);
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.65f), 20f);
    }

    private static bool IsInsideArrow(int x, int y, int w, int h)
    {
        if (x < 0 || y < 0 || x >= w || y >= h)
            return false;

        var t = 1f - y / (float)(h - 1);
        var halfWidth = Mathf.Max(1, Mathf.RoundToInt(t * (w * 0.5f)));
        var cx = w / 2;
        return Mathf.Abs(x - cx) <= halfWidth;
    }

    private static bool IsArrowBorderPixel(int x, int y, int w, int h)
    {
        if (!IsInsideArrow(x, y, w, h))
            return false;

        return !IsInsideArrow(x - 1, y, w, h)
               || !IsInsideArrow(x + 1, y, w, h)
               || !IsInsideArrow(x, y - 1, w, h)
               || !IsInsideArrow(x, y + 1, w, h);
    }
}
