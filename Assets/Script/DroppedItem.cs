using UnityEngine;

[RequireComponent(typeof(SpriteRenderer), typeof(CircleCollider2D))]
public class DroppedItem : MonoBehaviour
{
    [SerializeField] private float magnetRange = 2.5f;
    [SerializeField] private float pickupRange = 0.4f;
    [SerializeField] private float moveSpeed = 6f;

    private ItemData itemData;
    private int amount;
    private Transform playerTransform;
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        GetComponent<CircleCollider2D>().isTrigger = true;
    }

    public void Initialize(ItemData data, int count)
    {
        itemData = data;
        amount = count;
        if (data != null && data.icon != null)
        {
            spriteRenderer.sprite = data.icon;
        }

        transform.position += (Vector3)(Random.insideUnitCircle * 0.25f);

        PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    private void Update()
    {
        if (playerTransform == null || itemData == null) return;

        float distance = Vector2.Distance(transform.position, playerTransform.position);

        if (distance <= pickupRange)
        {
            Collect();
        }
        else if (distance <= magnetRange)
        {
            transform.position = Vector2.MoveTowards(
                transform.position,
                playerTransform.position,
                moveSpeed * Time.deltaTime
            );
        }
    }

    private void Collect()
    {
        if (InventoryManager.Instance == null) return;

        int leftover = InventoryManager.Instance.AddItem(itemData, amount);
        if (leftover <= 0)
        {
            Destroy(gameObject);
        }
        else
        {
            amount = leftover; // 가방이 꽉 차서 남은 개수 유지
        }
    }
}