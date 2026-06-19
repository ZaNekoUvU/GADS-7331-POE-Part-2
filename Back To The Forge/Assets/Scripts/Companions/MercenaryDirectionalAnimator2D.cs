using UnityEngine;

/// <summary>
/// Walk sheet layout: 3 columns = Right, Down, Left. 4 rows = walk frames (top to bottom).
/// Row 3 is typically a shorter/up-facing pose — used for Up when moving vertically.
/// Grid NPCs use step-synced frames; followers use timed playback.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class MercenaryDirectionalAnimator2D : MonoBehaviour
{
    public enum Facing
    {
        Down,
        Right,
        Left,
        Up
    }

    private enum DriveMode
    {
        GridStep,
        FreeTime
    }

    [SerializeField] private float framesPerSecond = 12f;

    private SpriteRenderer _spriteRenderer;
    private Sprite[] _down;
    private Sprite[] _right;
    private Sprite[] _left;
    private Sprite[] _up;

    private Facing _facing = Facing.Down;
    private DriveMode _driveMode = DriveMode.GridStep;
    private bool _moving;
    private float _frameTimer;
    private int _frameIndex;

    public Facing CurrentFacing => _facing;
    public bool IsMoving => _moving;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Configure(Texture2D walkSheet, int columns = 3, int rows = 4, float pixelsPerUnit = 100f)
    {
        if (walkSheet == null)
            return;

        SliceWalkSheet(walkSheet, columns, rows, pixelsPerUnit);
        _facing = Facing.Down;
        _moving = false;
        _frameIndex = 0;
        _frameTimer = 0f;
        _driveMode = DriveMode.GridStep;

        if (_spriteRenderer != null)
            _spriteRenderer.flipX = false;

        ApplyFrame(force: true);
    }

    /// <summary>One grid step: t is 0 at start, 1 at end. Frame swaps halfway for a consistent stride.</summary>
    public void SetGridStep(float t, Vector2 stepDirection)
    {
        _driveMode = DriveMode.GridStep;
        SetFacingFromDirection(stepDirection);

        _moving = true;
        _frameIndex = t < 0.5f ? 0 : 1;
        ApplyFrame(force: true);
    }

    public void SetGridIdle(Vector2 facingDirection)
    {
        _driveMode = DriveMode.GridStep;
        if (facingDirection.sqrMagnitude > 0.0001f)
            SetFacingFromDirection(facingDirection);

        _moving = false;
        _frameIndex = 0;
        _frameTimer = 0f;
        ApplyFrame(force: true);
    }

    public void SetFacing(Facing facing)
    {
        if (_facing == facing)
            return;

        _facing = facing;
        _frameIndex = 0;
        _frameTimer = 0f;
        ApplyFrame(force: true);
    }

    public void SetFacingFromDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
            return;

        var d = direction.normalized;
        Facing next;
        if (Mathf.Abs(d.x) > Mathf.Abs(d.y))
            next = d.x < 0f ? Facing.Left : Facing.Right;
        else
            next = d.y > 0f ? Facing.Up : Facing.Down;

        if (_spriteRenderer != null)
            _spriteRenderer.flipX = false;

        if (_facing != next)
        {
            _facing = next;
            _frameIndex = 0;
            _frameTimer = 0f;
            ApplyFrame(force: true);
        }
    }

    public void SetMoving(bool moving)
    {
        _driveMode = DriveMode.FreeTime;
        if (_moving == moving)
            return;

        _moving = moving;
        if (!moving)
        {
            _frameIndex = 0;
            _frameTimer = 0f;
            ApplyFrame(force: true);
        }
    }

    private void OnEnable()
    {
        _moving = false;
        _frameIndex = 0;
        _frameTimer = 0f;
        ApplyFrame(force: true);
    }

    private void Update()
    {
        if (_driveMode != DriveMode.FreeTime || _down == null || _down.Length == 0)
            return;

        AdvanceTimedFrame();
        ApplyFrame();
    }

    private void AdvanceTimedFrame()
    {
        if (!_moving)
            return;

        var frames = GetFrames(_facing);
        if (frames == null || frames.Length <= 1)
            return;

        _frameTimer += Time.deltaTime;
        var frameDuration = 1f / Mathf.Max(1f, framesPerSecond);
        while (_frameTimer >= frameDuration)
        {
            _frameTimer -= frameDuration;
            _frameIndex = (_frameIndex + 1) % frames.Length;
        }
    }

    private void ApplyFrame(bool force = false)
    {
        var frames = GetFrames(_facing);
        if (frames == null || frames.Length == 0 || _spriteRenderer == null)
            return;

        var index = _moving ? _frameIndex : 0;
        index = Mathf.Clamp(index, 0, frames.Length - 1);

        if (!force && _spriteRenderer.sprite == frames[index])
            return;

        _spriteRenderer.flipX = false;
        _spriteRenderer.sprite = frames[index];
    }

    private Sprite[] GetFrames(Facing facing)
    {
        return facing switch
        {
            Facing.Down => _down,
            Facing.Right => _right,
            Facing.Left => _left,
            Facing.Up => _up,
            _ => _down
        };
    }

    private void SliceWalkSheet(Texture2D walkSheet, int columns, int rows, float pixelsPerUnit)
    {
        if (!walkSheet.isReadable)
        {
            Debug.LogWarning(
                $"{nameof(MercenaryDirectionalAnimator2D)}: Walk sheet '{walkSheet.name}' is not Read/Write enabled. " +
                "Use Back To The Forge > Mercenaries > Enable Walk Sheet Read/Write.",
                this);
            return;
        }

        columns = Mathf.Max(1, columns);
        rows = Mathf.Max(1, rows);

        var cellW = walkSheet.width / (float)columns;
        var cellH = walkSheet.height / (float)rows;

        // Columns: 0 = Right, 1 = Down, 2 = Left. Rows 0–2 = walk stride; row 3 = up / compact pose.
        _right = SliceDirection(walkSheet, 0, new[] { 0, 2 }, 3, rows, cellW, cellH, pixelsPerUnit);
        _down = SliceDirection(walkSheet, 1, new[] { 0, 2 }, 3, rows, cellW, cellH, pixelsPerUnit);
        _left = SliceDirection(walkSheet, 2, new[] { 0, 2 }, 3, rows, cellW, cellH, pixelsPerUnit);
        _up = SliceDirection(walkSheet, 1, new[] { 2, 3 }, 3, rows, cellW, cellH, pixelsPerUnit);
    }

    private static Sprite[] SliceDirection(
        Texture2D sheet,
        int column,
        int[] frameRowsFromTop,
        int columns,
        int rows,
        float cellW,
        float cellH,
        float pixelsPerUnit)
    {
        var trimRects = new Rect[frameRowsFromTop.Length];
        var valid = new bool[frameRowsFromTop.Length];

        for (var i = 0; i < frameRowsFromTop.Length; i++)
        {
            var unityRow = rows - 1 - frameRowsFromTop[i];
            var cellRect = new Rect(column * cellW, unityRow * cellH, cellW, cellH);
            if (TryGetTrimmedRect(sheet, cellRect, out trimRects[i]))
                valid[i] = true;
        }

        var footX = 0f;
        var footY = float.MaxValue;
        var count = 0;
        for (var i = 0; i < trimRects.Length; i++)
        {
            if (!valid[i])
                continue;

            var r = trimRects[i];
            footX += r.x + r.width * 0.5f;
            footY = Mathf.Min(footY, r.y);
            count++;
        }

        if (count == 0)
            return System.Array.Empty<Sprite>();

        footX /= count;

        var sprites = new Sprite[frameRowsFromTop.Length];
        for (var i = 0; i < frameRowsFromTop.Length; i++)
        {
            if (!valid[i])
                continue;

            var trim = trimRects[i];
            var pivot = new Vector2(
                Mathf.Clamp((footX - trim.x) / trim.width, 0f, 1f),
                Mathf.Clamp((footY - trim.y) / trim.height, 0f, 1f));
            sprites[i] = Sprite.Create(sheet, trim, pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        return sprites;
    }

    private static bool TryGetTrimmedRect(Texture2D sheet, Rect cellRect, out Rect trimRect)
    {
        trimRect = default;

        var x = Mathf.Clamp(Mathf.FloorToInt(cellRect.x), 0, sheet.width - 1);
        var y = Mathf.Clamp(Mathf.FloorToInt(cellRect.y), 0, sheet.height - 1);
        var w = Mathf.Clamp(Mathf.CeilToInt(cellRect.width), 1, sheet.width - x);
        var h = Mathf.Clamp(Mathf.CeilToInt(cellRect.height), 1, sheet.height - y);

        var pixels = sheet.GetPixels(x, y, w, h);
        var minX = w;
        var minY = h;
        var maxX = -1;
        var maxY = -1;

        for (var py = 0; py < h; py++)
        {
            for (var px = 0; px < w; px++)
            {
                if (pixels[py * w + px].a <= 0.02f)
                    continue;

                if (px < minX) minX = px;
                if (py < minY) minY = py;
                if (px > maxX) maxX = px;
                if (py > maxY) maxY = py;
            }
        }

        if (maxX < 0)
            return false;

        trimRect = new Rect(x + minX, y + minY, maxX - minX + 1, maxY - minY + 1);
        return trimRect.width > 0f && trimRect.height > 0f;
    }

    /// <summary>World-space height of the down-facing frame for scaling.</summary>
    public float GetReferenceWorldHeight(float pixelsPerUnit)
    {
        if (_down == null || _down.Length == 0 || _down[0] == null)
            return 0f;

        return _down[0].bounds.size.y;
    }
}
