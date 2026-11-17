using UnityEngine;

public class PlayerMining : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ChunkManager chunkManager;

    [Header("Mining Settings")]
    [SerializeField] private float miningRange = 2.5f; // 채광 가능 사거리 (타일 단위)

    private Camera mainCamera;

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    private void Update()
    {
        // 마우스 왼쪽 버튼 클릭(또는 누르고 있기: GetMouseButton) 감지
        if (Input.GetMouseButtonDown(0))
        {
            AttemptMineAtMouse();
        }
    }

    private void AttemptMineAtMouse()
    {
        if (chunkManager == null || mainCamera == null) return;

        // 마우스 화면 좌표를 월드 좌표로 변환
        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = -mainCamera.transform.position.z;
        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(mouseScreenPos);

        // 클릭한 위치의 정수 타일 좌표 계산
        Vector2Int targetTilePos = new Vector2Int(
            Mathf.FloorToInt(mouseWorldPos.x),
            Mathf.FloorToInt(mouseWorldPos.y)
        );

        // 타일 정중앙(+0.5f)과 플레이어 사이의 거리 검사
        Vector2 tileCenter = new Vector2(targetTilePos.x + 0.5f, targetTilePos.y + 0.5f);
        if (Vector2.Distance(transform.position, tileCenter) > miningRange)
        {
            return; // 사거리 밖이면 채광 불가
        }

        chunkManager.TryMineTile(targetTilePos);
    }
}