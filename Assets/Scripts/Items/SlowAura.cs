using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(WorldItem))]
public class SlowAura : MonoBehaviour
{
    [SerializeField] private float radius = 3f;
    [Range(0f, 1f)]
    [SerializeField] private float slowPercent = 0.5f;
    [SerializeField] private LayerMask heroLayers = ~0;

    private WorldItem worldItem;
    private StatModifier[] slowModifiers;

    private readonly HashSet<StatController> affected = new();
    private readonly List<StatController> inRange = new();

    private void Awake()
    {
        worldItem = GetComponent<WorldItem>();

        slowModifiers = new[]
        {
            new StatModifier(
                StatType.MoveSpeed,
                ModifierOp.Percent,
                -slowPercent
            )
        };
    }

    private void Update()
    {
        if (worldItem == null || !worldItem.IsServer)
            return;

        if (!worldItem.IsLanded)
        {
            Clear();
            return;
        }

        CollectInRange();

        for (int i = 0; i < inRange.Count; i++)
        {
            StatController controller = inRange[i];

            if (affected.Add(controller))
                controller.AddSource(this, slowModifiers);
        }

        RemoveOutOfRange();
    }

    private void CollectInRange()
    {
        inRange.Clear();

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            radius,
            heroLayers
        );

        foreach (Collider2D hit in hits)
        {
            StatController controller =
                hit.GetComponentInParent<StatController>();

            if (controller == null || inRange.Contains(controller))
                continue;

            inRange.Add(controller);
        }
    }

    private void RemoveOutOfRange()
    {
        affected.RemoveWhere(controller =>
        {
            if (controller != null && inRange.Contains(controller))
                return false;

            controller?.RemoveSource(this);
            return true;
        });
    }

    private void Clear()
    {
        if (affected.Count == 0)
            return;

        foreach (StatController controller in affected)
        {
            if (controller != null)
                controller.RemoveSource(this);
        }

        affected.Clear();
    }

    private void OnDisable()
    {
        Clear();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0f, 0.8f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
