using UnityEngine;

/// <summary>
/// Helpers for NPCs that use a solid collider for movement and a separate trigger for talk range.
/// </summary>
public static class Collider2DTriggerUtil
{
    public static bool HasTriggerCollider(GameObject go)
    {
        if (go == null)
            return false;

        foreach (var c in go.GetComponents<Collider2D>())
        {
            if (c != null && c.isTrigger)
                return true;
        }

        return false;
    }

    public static void WarnIfNoTalkTrigger(GameObject go, string componentName)
    {
        if (go != null && !HasTriggerCollider(go))
            Debug.LogWarning($"{componentName}: add a trigger Collider2D on '{go.name}' for talk range.", go);
    }
}
