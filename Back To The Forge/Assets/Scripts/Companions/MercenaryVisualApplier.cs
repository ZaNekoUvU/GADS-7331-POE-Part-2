using UnityEngine;

/// <summary>
/// Applies mercenary walk sheets in exploration and battle-ready sprites in combat.
/// </summary>
public static class MercenaryVisualApplier
{
    public const float ExplorationTargetHeight = 0.95f;
    public const float CombatTargetHeight = 1.05f;

    public static bool ApplyExplorationVisual(GameObject root, HireableCompanionOffer offer, Color? tint = null)
    {
        if (root == null || offer == null || !offer.HasWalkVisuals)
            return false;

        var spriteRenderer = root.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = root.AddComponent<SpriteRenderer>();

        spriteRenderer.color = tint ?? Color.white;

        var animator = root.GetComponent<MercenaryDirectionalAnimator2D>();
        if (animator == null)
            animator = root.AddComponent<MercenaryDirectionalAnimator2D>();

        animator.Configure(offer.WalkSpritesheet, offer.WalkSheetColumns, offer.WalkSheetRows, offer.SpritePixelsPerUnit);

        var legacyAnimator = root.GetComponent<Animator>();
        if (legacyAnimator != null)
            legacyAnimator.enabled = false;

        EnsureExplorationPhysics(root);

        var refHeight = animator.GetReferenceWorldHeight(offer.SpritePixelsPerUnit);
        if (refHeight > 0.001f)
        {
            var scale = ExplorationTargetHeight / refHeight;
            root.transform.localScale = new Vector3(scale, scale, 1f);
        }

        return true;
    }

    public static void ApplyCombatVisual(CombatUnit unit, HireableCompanionOffer offer)
    {
        if (unit == null || offer == null || offer.BattleReadySprite == null)
            return;

        var spriteRenderer = unit.SpriteRenderer;
        if (spriteRenderer == null)
            return;

        spriteRenderer.color = Color.white;
        spriteRenderer.sprite = offer.BattleReadySprite;
        FitScaleToCurrentSprite(unit.gameObject, CombatTargetHeight);
    }

    public static void ApplyEnemyCombatVisual(CombatUnit unit, Sprite battleReadySprite)
    {
        if (unit == null || battleReadySprite == null)
            return;

        var spriteRenderer = unit.SpriteRenderer;
        if (spriteRenderer == null)
            return;

        spriteRenderer.color = Color.white;
        spriteRenderer.sprite = battleReadySprite;
        FitScaleToCurrentSprite(unit.gameObject, CombatTargetHeight);
    }

    public static void EnsureExplorationPhysics(GameObject root)
    {
        var body = root.GetComponent<Rigidbody2D>();
        if (body == null)
            body = root.AddComponent<Rigidbody2D>();

        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.simulated = true;
    }

    public static void FitScaleToCurrentSprite(GameObject root, float targetHeight)
    {
        var spriteRenderer = root.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null || spriteRenderer.sprite == null || targetHeight <= 0f)
            return;

        var height = spriteRenderer.sprite.bounds.size.y;
        if (height <= 0.001f)
            return;

        var scale = targetHeight / height;
        root.transform.localScale = new Vector3(scale, scale, 1f);
    }
}
