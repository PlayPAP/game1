using UnityEngine;

public class PlayerMining : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ChunkManager chunkManager;

    [Header("Mining Settings")]
    [SerializeField] private float miningRange = 2.5f;
    [SerializeField] private int miningPower = 1;       // 한 번 타격당 입히는 대미지
    [SerializeField] private float mineInterval = 0.3f; // 연속 채광 간격(초)

    private Camera mainCamera;
    private float nextMineTime;

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    private void Update()
    {
        // 마우스 좌클릭 유지 시 쿨타임마다 채광 시도
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

        Vector2 tileCenter = new Vector2(targetTilePos.x + 0.5f, targetTilePos.y + 0.5f);
        if (Vector2.Distance(transform.position, tileCenter) > miningRange)
        {
            return false;
        }

        int currentPower = miningPower; // 기본 맨손 채광력
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