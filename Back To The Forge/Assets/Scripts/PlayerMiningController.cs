using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Put on the player (needs <see cref="Rigidbody2D"/> + non-trigger collider). Mines the nearest <see cref="IronVein"/>
/// while Interact is held: one ore per full second (scaled time).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMiningController : MonoBehaviour
{
    [SerializeField] private InputActionReference interactAction;
    [SerializeField] private Inventory inventory;

    private readonly HashSet<IronVein> _veinsInRange = new();
    private float _mineAccumulator;

    private void Awake()
    {
        if (inventory == null)
            inventory = GetComponent<Inventory>();
    }

    private void OnEnable()
    {
        if (interactAction != null)
            interactAction.action.Enable();
    }

    private void OnDisable()
    {
        if (interactAction != null)
            interactAction.action.Disable();

        _mineAccumulator = 0f;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var vein = other.GetComponent<IronVein>();
        if (vein != null)
            _veinsInRange.Add(vein);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var vein = other.GetComponent<IronVein>();
        if (vein != null)
            _veinsInRange.Remove(vein);
    }

    private void Update()
    {
        if (interactAction == null || inventory == null)
            return;

        var vein = GetClosestVein();
        if (vein == null || !vein.HasOreLeft)
        {
            _mineAccumulator = 0f;
            return;
        }

        if (!interactAction.action.IsPressed())
        {
            _mineAccumulator = 0f;
            return;
        }

        _mineAccumulator += Time.deltaTime;

        while (_mineAccumulator >= 1f)
        {
            _mineAccumulator -= 1f;

            vein = GetClosestVein();
            if (vein == null || !vein.HasOreLeft)
                break;

            var leftover = inventory.TryAdd(vein.OreDefinition, vein.OrePerTick);
            if (leftover > 0)
            {
                Debug.LogWarning("Inventory full — cannot add more ore.", this);
                break;
            }

            vein.RegisterSuccessfulMine();
        }
    }

    private IronVein GetClosestVein()
    {
        IronVein best = null;
        var bestSqr = float.PositiveInfinity;
        var p = transform.position;

        _veinsInRange.RemoveWhere(v => v == null);

        foreach (var vein in _veinsInRange)
        {
            if (vein == null || !vein.HasOreLeft)
                continue;

            var d = (vein.transform.position - p).sqrMagnitude;
            if (d < bestSqr)
            {
                bestSqr = d;
                best = vein;
            }
        }

        return best;
    }
}
