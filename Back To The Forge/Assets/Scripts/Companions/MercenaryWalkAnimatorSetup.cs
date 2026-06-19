using UnityEngine;

/// <summary>
/// Marks NPCs that use a 4-direction walk animator (Down, Right, Left, Up) instead of Left + flipX.
/// </summary>
public sealed class MercenaryWalkAnimatorSetup : MonoBehaviour
{
    [SerializeField] private bool useDedicatedRightWalk = true;

    public bool UseDedicatedRightWalk => useDedicatedRightWalk;

    public void Configure(bool dedicatedRightWalk)
    {
        useDedicatedRightWalk = dedicatedRightWalk;
    }
}
