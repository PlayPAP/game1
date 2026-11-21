using UnityEngine;

public class PlayerMining : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ChunkManager chunkManager;

    [Header("Mining Settings")]
    [SerializeField] private float miningRange = 2.5f;
    [SerializeField] private int miningPower = 1;
    [SerializeField] private float mineInterval = 0.3f;

    private Camera mainCamera;
    private float nextMineTime;

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (Input.GetMouseButton(0) && Time.time >= nextMineTime)
        {
            if (AttemptMineAtMouse())
            {
                nextMineTime = Time.time + mineInterval;
            }
        }
    }

    private bool AttemptMineAtMouse()
    {
        if (chunkManager == null || mainCamera == null) return false;

        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = -mainCamera.transform.position.z;
        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(mouseScreenPos);

        Vector2Int targetTilePos = new Vector2Int(
            Mathf.FloorToInt(mouseWorldPos.x),
            Mathf.FloorToInt(mouseWorldPos.y)
        );

        // 위로 0.45칸 돌출된 벽 윗면을 클릭한 경우 아래 칸의 벽으로 보정
        if (!chunkManager.HasMineableTile(targetTilePos))
        {
            Vector2Int belowPos = new Vector2Int(targetTilePos.x, Mathf.FloorToInt(mouseWorldPos.y - 0.45f));
            if (chunkManager.HasMineableTile(belowPos))
            {
                targetTilePos = belowPos;
            }
        }

        Vector2 tileCenter = new Vector2(targetTilePos.x + 0.5f, targetTilePos.y + 0.5f);
        if (Vector2.Distance(transform.position, tileCenter) > miningRange)
            return false;

        int currentPower = miningPower;
        if (InventoryManager.Instance != null)
        {
            ItemData equipped = InventoryManager.Instance.GetSelectedItem();
            if (equipped != null && equipped.itemType == ItemType.Pickaxe)
            {
                currentPower = equipped.miningPower;
            }
        }

        return chunkManager.DamageTile(targetTilePos, currentPower);
    }
}