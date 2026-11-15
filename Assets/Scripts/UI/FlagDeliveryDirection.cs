using UnityEngine;

// Local-only world-space pointer. Assign a sprite pointing UP in the Inspector.
public class FlagDeliveryDirection : MonoBehaviour
{
    [SerializeField] private Sprite arrowSprite;
    [SerializeField] private Color arrowColor = Color.white;
    [SerializeField, Min(0f)] private float distanceFromFeet = 0.7f;
    [SerializeField, Min(0.01f)] private float size = 0.5f;
    [SerializeField] private Vector2 feetOffset;
    [SerializeField] private float spriteAngleOffset = -90f;
    [SerializeField] private string behindHeroLayer = "HeroBehind";
    [SerializeField] private string inFrontOfHeroLayer = "Effect";
    [SerializeField] private int sortingOrder;

    private SpriteRenderer arrow;

    private void LateUpdate()
    {
        if (!TryGetDirection(out Vector3 feet, out Vector2 direction))
        {
            if (arrow != null)
                arrow.enabled = false;
            return;
        }

        if (arrow == null)
        {
            GameObject visual = new GameObject("Local Flag Delivery Arrow");
            arrow = visual.AddComponent<SpriteRenderer>();
        }

        arrow.enabled = true;
        arrow.sprite = arrowSprite;
        arrow.color = arrowColor;
        arrow.sortingLayerName = direction.y > 0f
            ? behindHeroLayer : inFrontOfHeroLayer;
        arrow.sortingOrder = sortingOrder;
        arrow.transform.position = feet + (Vector3)feetOffset +
            (Vector3)(direction * distanceFromFeet);
        arrow.transform.rotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + spriteAngleOffset);
        arrow.transform.localScale = Vector3.one * size;
    }

    private bool TryGetDirection(out Vector3 feet, out Vector2 direction)
    {
        feet = default;
        direction = default;
        PlayerNetwork player = PlayerNetwork.LocalPlayer;
        if (arrowSprite == null || player == null || !player.IsSpawned ||
            player.GetComponent<Inventory>() is not Inventory inventory)
            return false;

        FlagObject flag = FindAnyObjectByType<FlagObject>();
        FlagDeliveryZone delivery = FindAnyObjectByType<FlagDeliveryZone>();
        if (flag == null || delivery == null || !flag.IsSpawned ||
            !delivery.IsSpawned || !flag.IsCarried ||
            flag.CarrierClientId != player.OwnerClientId ||
            inventory.GetItem(inventory.SelectedSlot.Value) != flag.InventoryItem)
            return false;

        feet = player.Body != null ? player.Body.position : player.transform.position;
        direction = (Vector2)(delivery.DeliveryPoint.position - feet);
        if (direction.sqrMagnitude < 0.0001f)
            return false;

        direction.Normalize();
        return true;
    }

    private void OnDestroy()
    {
        if (arrow != null)
            Destroy(arrow.gameObject);
    }
}
