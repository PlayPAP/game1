using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class ChunkManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform gridParent;
    [SerializeField] private GameObject chunkPrefab;
    [SerializeField] private GameObject droppedItemPrefab;

    [Header("Tiles & Mineable Data")]
    [SerializeField] private TileBase groundTile;
    [SerializeField] private TileBase wallTile;
    [SerializeField] private TileBase oreTile; // 광석 타일 (선택 사항)
    [SerializeField] private List<TileData> mineableTileList = new List<TileData>();

    [Header("World Settings")]
    [SerializeField] private int chunkSize = 16;
    [SerializeField] private int loadRadius = 1;
    [SerializeField] private float noiseScale = 0.08f;
    [SerializeField] private int seed = 12345;

    private Dictionary<Vector2Int, GameObject> spawnedChunks = new Dictionary<Vector2Int, GameObject>();
    private Dictionary<Vector2Int, Tilemap> chunkTilemaps = new Dictionary<Vector2Int, Tilemap>();

    // 타일 에셋별 내구도/드롭 정보 조회용 딕셔너리
    private Dictionary<TileBase, TileData> tileDataLookup = new Dictionary<TileBase, TileData>();

    // 변경된 타일 및 현재 남은 내구도 저장 (월드 절대 좌표 기준)
    private Dictionary<Vector2Int, TileBase> modifiedTiles = new Dictionary<Vector2Int, TileBase>();
    private Dictionary<Vector2Int, int> currentTileHealth = new Dictionary<Vector2Int, int>();

    private Vector2Int lastPlayerChunk = new Vector2Int(int.MaxValue, int.MaxValue);

    private void Awake()
    {
        foreach (TileData data in mineableTileList)
        {
            if (data != null && data.tileAsset != null && !tileDataLookup.ContainsKey(data.tileAsset))
            {
                tileDataLookup.Add(data.tileAsset, data);
            }
        }
    }

    private void Update()
    {
        if (player == null) return;

        Vector2Int currentChunk = WorldToChunkCoord(player.position);

        if (currentChunk != lastPlayerChunk)
        {
            lastPlayerChunk = currentChunk;
            UpdateChunks(currentChunk);
        }
    }

    public Vector2Int WorldToChunkCoord(Vector3 worldPos)
    {
        int x = Mathf.FloorToInt(worldPos.x / chunkSize);
        int y = Mathf.FloorToInt(worldPos.y / chunkSize);
        return new Vector2Int(x, y);
    }

    private void UpdateChunks(Vector2Int centerChunk)
    {
        HashSet<Vector2Int> neededChunks = new HashSet<Vector2Int>();

        for (int x = -loadRadius; x <= loadRadius; x++)
        {
            for (int y = -loadRadius; y <= loadRadius; y++)
            {
                Vector2Int coord = new Vector2Int(centerChunk.x + x, centerChunk.y + y);
                neededChunks.Add(coord);

                if (!spawnedChunks.ContainsKey(coord))
                {
                    CreateChunk(coord);
                }
                else if (!spawnedChunks[coord].activeSelf)
                {
                    spawnedChunks[coord].SetActive(true);
                }
            }
        }

        foreach (var pair in spawnedChunks)
        {
            if (!neededChunks.Contains(pair.Key) && pair.Value.activeSelf)
            {
                pair.Value.SetActive(false);
            }
        }
    }

    private void CreateChunk(Vector2Int chunkCoord)
    {
        Vector3 worldPosition = new Vector3(chunkCoord.x * chunkSize, chunkCoord.y * chunkSize, 0f);
        GameObject chunkObj = Instantiate(chunkPrefab, worldPosition, Quaternion.identity, gridParent);
        chunkObj.name = $"Chunk_{chunkCoord.x}_{chunkCoord.y}";

        Tilemap tilemap = chunkObj.GetComponent<Tilemap>();
        GenerateTerrain(tilemap, chunkCoord);

        spawnedChunks.Add(chunkCoord, chunkObj);
        chunkTilemaps.Add(chunkCoord, tilemap);
    }

    private void GenerateTerrain(Tilemap tilemap, Vector2Int chunkCoord)
    {
        BoundsInt bounds = new BoundsInt(0, 0, 0, chunkSize, chunkSize, 1);
        TileBase[] tiles = new TileBase[chunkSize * chunkSize];

        for (int y = 0; y < chunkSize; y++)
        {
            for (int x = 0; x < chunkSize; x++)
            {
                int globalX = chunkCoord.x * chunkSize + x;
                int globalY = chunkCoord.y * chunkSize + y;
                Vector2Int globalPos = new Vector2Int(globalX, globalY);

                // 1순위: 이미 채광되거나 수정된 타일
                if (modifiedTiles.TryGetValue(globalPos, out TileBase savedTile))
                {
                    tiles[y * chunkSize + x] = savedTile;
                    continue;
                }

                // 2순위: 시작 지점 안전 구역
                if (Mathf.Abs(globalX) <= 2 && Mathf.Abs(globalY) <= 2)
                {
                    tiles[y * chunkSize + x] = groundTile;
                    continue;
                }

                // 3순위: 노이즈 기반 벽 및 광맥 생성
                float wallNoise = Mathf.PerlinNoise((globalX + seed) * noiseScale, (globalY + seed) * noiseScale);
                if (wallNoise > 0.45f)
                {
                    // 시드 오프셋을 준 별도 노이즈로 광석 분포 결정
                    float oreNoise = Mathf.PerlinNoise((globalX + seed + 500) * 0.15f, (globalY + seed + 500) * 0.15f);
                    if (oreTile != null && oreNoise > 0.72f)
                    {
                        tiles[y * chunkSize + x] = oreTile;
                    }
                    else
                    {
                        tiles[y * chunkSize + x] = wallTile;
                    }
                }
                else
                {
                    tiles[y * chunkSize + x] = groundTile;
                }
            }
        }

        tilemap.SetTilesBlock(bounds, tiles);
    }

    // 타일에 대미지를 입히고 파괴 시 아이템을 드롭하는 메서드
    public bool DamageTile(Vector2Int globalTilePos, int damage)
    {
        Vector2Int chunkCoord = new Vector2Int(
            Mathf.FloorToInt((float)globalTilePos.x / chunkSize),
            Mathf.FloorToInt((float)globalTilePos.y / chunkSize)
        );

        if (!chunkTilemaps.TryGetValue(chunkCoord, out Tilemap targetTilemap))
            return false;

        int localX = globalTilePos.x - (chunkCoord.x * chunkSize);
        int localY = globalTilePos.y - (chunkCoord.y * chunkSize);
        Vector3Int localPos = new Vector3Int(localX, localY, 0);

        TileBase currentTile = targetTilemap.GetTile(localPos);
        if (currentTile == null || !tileDataLookup.TryGetValue(currentTile, out TileData data))
            return false; // 채광 불가능한 타일(바닥 등)

        // 현재 남은 체력 조회 (처음 때리는 타일이면 maxHealth로 초기화)
        if (!currentTileHealth.TryGetValue(globalTilePos, out int hp))
        {
            hp = data.maxHealth;
        }

        hp -= damage;

        if (hp > 0)
        {
            // 아직 부서지지 않음 -> 체력 갱신 및 타일 색상을 어둡게 변경해 타격 피드백 제공
            currentTileHealth[globalTilePos] = hp;
            float healthRatio = (float)hp / data.maxHealth;
            targetTilemap.SetTileFlags(localPos, TileFlags.None);
            targetTilemap.SetColor(localPos, Color.Lerp(new Color(0.45f, 0.45f, 0.45f), Color.white, healthRatio));
        }
        else
        {
            // 내구도 0 이하 -> 타일 파괴 및 색상 초기화
            currentTileHealth.Remove(globalTilePos);
            targetTilemap.SetTileFlags(localPos, TileFlags.None);
            targetTilemap.SetColor(localPos, Color.white);
            targetTilemap.SetTile(localPos, groundTile);
            modifiedTiles[globalTilePos] = groundTile;

            // 광물/자원 아이템 드롭
            SpawnDroppedItem(globalTilePos, data);
        }

        return true;
    }

    private void SpawnDroppedItem(Vector2Int globalTilePos, TileData data)
    {
        if (droppedItemPrefab == null || data.dropItem == null) return;

        Vector3 spawnPos = new Vector3(globalTilePos.x + 0.5f, globalTilePos.y + 0.5f, 0f);
        GameObject itemObj = Instantiate(droppedItemPrefab, spawnPos, Quaternion.identity);

        if (itemObj.TryGetComponent(out DroppedItem droppedItem))
        {
            droppedItem.Initialize(data.dropItem, data.dropAmount);
        }
    }
}