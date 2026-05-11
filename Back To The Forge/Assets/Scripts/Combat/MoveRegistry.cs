using UnityEngine;

/// <summary>
/// Maps move id → <see cref="MoveDefinition"/> for resolving damage and UI names.
/// </summary>
[CreateAssetMenu(fileName = "MoveRegistry", menuName = "Combat/Move Registry")]
public class MoveRegistry : ScriptableObject
{
    [SerializeField] private MoveDefinition[] moves;

    public bool TryGet(int moveId, out MoveDefinition move)
    {
        if (moves != null)
        {
            foreach (var m in moves)
            {
                if (m != null && m.MoveId == moveId)
                {
                    move = m;
                    return true;
                }
            }
        }

        move = null;
        return false;
    }

    public MoveDefinition[] AllMoves => moves;
}
