using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class ChunkManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform gridParent;
    [SerializeField] private GameObject chunkPrefab;

    [Header("Tiles")]
    [SerializeField] private TileBase groundTile;
    [SerializeField] private TileBase wallTile;

    [Header("World Settings")]
    [SerializeField] private int chunkSize = 16;
    [SerializeField] private int loadRadius = 1;
    [SerializeField] private float noiseScale = 0.08f;
    [SerializeField] private int seed = 12345;

    private Dictionary<Vector2Int, GameObject> spawnedChunks = new Dictionary<Vector2Int, GameObject>();
    private Dictionary<Vector2Int, Tilemap> chunkTilemaps = new Dictionary<Vector2Int, Tilemap>();

    // 플레이어가 채광하거나 설치해 변경된 타일 데이터 저장소 (월드 절대 좌표 -> 변경된 타일)
    private Dictionary<Vector2Int, TileBase> modifiedTiles = new Dictionary<Vector2Int, TileBase>();

    private Vector2Int lastPlayerChunk = new Vector2Int(int.MaxValue, int.MaxValue);

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

                // 1순위: 플레이어가 채광/수정한 이력이 있으면 해당 타일 상태 우선 적용
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

                // 3순위: 펄린 노이즈 기반 기본 지형 생성
                float noise = Mathf.PerlinNoise((globalX + seed) * noiseScale, (globalY + seed) * noiseScale);
                tiles[y * chunkSize + x] = noise > 0.45f ? wallTile : groundTile;
            }
        }

        tilemap.SetTilesBlock(bounds, tiles);
    }

    // 지정된 월드 타일 좌표의 벽을 채광하는 메서드
    public bool TryMineTile(Vector2Int globalTilePos)
    {
        Vector2Int chunkCoord = new Vector2Int(
            Mathf.FloorToInt((float)globalTilePos.x / chunkSize),
            Mathf.FloorToInt((float)globalTilePos.y / chunkSize)
        );

        // 해당 청크가 현재 로드되어 있지 않으면 무시
        if (!chunkTilemaps.TryGetValue(chunkCoord, out Tilemap targetTilemap))
            return false;

        // 월드 절대 좌표를 청크 내부 로컬 좌표(0 ~ chunkSize-1)로 변환
        int localX = globalTilePos.x - (chunkCoord.x * chunkSize);
        int localY = globalTilePos.y - (chunkCoord.y * chunkSize);
        Vector3Int localPos = new Vector3Int(localX, localY, 0);

        TileBase currentTile = targetTilemap.GetTile(localPos);

        // 현재 타일이 벽 타일일 때만 파괴 처리
        if (currentTile == wallTile)
        {
            targetTilemap.SetTile(localPos, groundTile);

            // 변경 이력 저장 (추후 세이브/로드 및 청크 재생성 시 유지됨)
            modifiedTiles[globalTilePos] = groundTile;
            return true;
        }

        return false;
    }
}