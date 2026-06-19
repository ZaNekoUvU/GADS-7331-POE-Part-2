using UnityEngine;

/// <summary>
/// Applies mercenary walk sheets in exploration and battle-ready sprites in combat.
/// </summary>
public static class MercenaryVisualApplier
{
    public const float ExplorationTargetHeight = 0.95f;
    public const float RecruiterCampTargetHeight = ExplorationTargetHeight;
    public const float CombatTargetHeight = 1.05f;

    public static bool ApplyExplorationVisual(
        GameObject root,
        HireableCompanionOffer offer,
        Color? tint = null,
        float targetHeight = ExplorationTargetHeight)
    {
        if (root == null || offer == null || !offer.HasWalkVisuals)
            return false;

        root.transform.localScale = Vector3.one;

        var spriteRenderer = root.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = root.AddComponent<SpriteRenderer>();

        spriteRenderer.color = tint ?? Color.white;

        if (offer.WalkAnimatorController != null)
            ApplyWalkAnimator(root, offer.WalkAnimatorController);
        else
            ApplyRuntimeWalkSheet(root, offer, targetHeight);

        EnsureExplorationPhysics(root);
        FitScaleToOfferExploration(root, offer, targetHeight);
        return true;
    }

    /// <summary>Consistent exploration height using the offer art (not whichever animator frame is active).</summary>
    public static void FitScaleToOfferExploration(
        GameObject root,
        HireableCompanionOffer offer,
        float targetHeight = ExplorationTargetHeight)
    {
        if (root == null || offer == null || targetHeight <= 0f)
            return;

        if (!TryGetExplorationReferenceHeight(root, offer, out var referenceHeight))
        {
            FitScaleToCurrentSprite(root, targetHeight);
            return;
        }

        root.transform.localScale = Vector3.one;
        var scale = targetHeight / referenceHeight;
        root.transform.localScale = new Vector3(scale, scale, 1f);
    }

    /// <summary>Walk-frame height for exploration — battle-ready portraits are too tall for this.</summary>
    public static bool TryGetExplorationReferenceHeight(
        GameObject root,
        HireableCompanionOffer offer,
        out float height)
    {
        height = 0f;
        if (offer == null)
            return false;

        var dirAnim = root != null ? root.GetComponent<MercenaryDirectionalAnimator2D>() : null;
        if (dirAnim != null)
        {
            height = dirAnim.GetReferenceWorldHeight(offer.SpritePixelsPerUnit);
            if (height > 0.001f)
                return true;
        }

        if (offer.WalkSpritesheet != null
            && MercenaryDirectionalAnimator2D.TryGetWalkReferenceWorldHeight(
                offer.WalkSpritesheet,
                offer.WalkSheetColumns,
                offer.WalkSheetRows,
                offer.SpritePixelsPerUnit,
                out height))
        {
            return true;
        }

        if (!offer.HasWalkVisuals && offer.BattleReadySprite != null)
        {
            height = offer.BattleReadySprite.bounds.size.y;
            return height > 0.001f;
        }

        return false;
    }

    private static void ApplyWalkAnimator(GameObject root, RuntimeAnimatorController controller)
    {
        var runtimeAnimator = root.GetComponent<MercenaryDirectionalAnimator2D>();
        if (runtimeAnimator != null)
            Object.Destroy(runtimeAnimator);

        var unityAnimator = root.GetComponent<Animator>();
        if (unityAnimator == null)
            unityAnimator = root.AddComponent<Animator>();

        unityAnimator.runtimeAnimatorController = controller;
        unityAnimator.enabled = true;

        var walkSetup = root.GetComponent<MercenaryWalkAnimatorSetup>();
        if (walkSetup == null)
            walkSetup = root.AddComponent<MercenaryWalkAnimatorSetup>();

        walkSetup.Configure(true);
    }

    private static void ApplyRuntimeWalkSheet(GameObject root, HireableCompanionOffer offer, float targetHeight)
    {
        var unityAnimator = root.GetComponent<Animator>();
        if (unityAnimator != null)
            unityAnimator.enabled = false;

        var walkSetup = root.GetComponent<MercenaryWalkAnimatorSetup>();
        if (walkSetup != null)
            Object.Destroy(walkSetup);

        var animator = root.GetComponent<MercenaryDirectionalAnimator2D>();
        if (animator == null)
            animator = root.AddComponent<MercenaryDirectionalAnimator2D>();

        animator.Configure(offer.WalkSpritesheet, offer.WalkSheetColumns, offer.WalkSheetRows, offer.SpritePixelsPerUnit);

        // Scale is applied once below via FitScaleToCurrentSprite.
    }

    public static void ApplyCombatVisual(CombatUnit unit, HireableCompanionOffer offer)
    {
        if (unit == null || offer == null || offer.BattleReadySprite == null)
            return;

        ApplyAllyCombatVisual(unit, offer.BattleReadySprite, faceRight: false);
    }

    /// <summary>Ally battle sprite on the field. Set <paramref name="faceRight"/> when the art faces left by default.</summary>
    public static void ApplyAllyCombatVisual(CombatUnit unit, Sprite sprite, bool faceRight)
    {
        if (unit == null || sprite == null)
            return;

        var spriteRenderer = unit.SpriteRenderer;
        if (spriteRenderer == null)
            return;

        spriteRenderer.color = Color.white;
        spriteRenderer.sprite = sprite;
        spriteRenderer.flipX = faceRight;
        spriteRenderer.flipY = false;
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

        root.transform.localScale = Vector3.one;

        var height = spriteRenderer.sprite.bounds.size.y;
        if (height <= 0.001f)
            return;

        var scale = targetHeight / height;
        root.transform.localScale = new Vector3(scale, scale, 1f);
    }
}
