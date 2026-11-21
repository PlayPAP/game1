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

    [System.Serializable]
    public class OreSpawnEntry
    {
        public string oreName;
        public TileBase oreTopTile;          // 광석 윗면 타일 (예: OreCopperTile)
        public TileBase oreFrontTile;        // 비워두면 아래 색상으로 앞면 광석 타일 자동 생성
        public Color32 oreSpeckColor = new Color32(218, 145, 72, 255); // 앞면 광석 알갱이 색
        [Range(0.5f, 0.95f)] public float noiseThreshold = 0.72f;      // 높을수록 희귀함
        public float noiseOffset = 500f;                               // 광맥별 노이즈 시드 분리값
        public float minDistanceFromOrigin = 0f;                       // 시작 지점(0,0) 최소 거리
    }

    [Header("Tiles & Mineable Data")]
    [SerializeField] private TileBase groundTile;
    [SerializeField] private TileBase wallTile;
    [SerializeField] private TileBase oreTile;           // 하위 호환용 유지
    [SerializeField] private TileBase wallFrontTile;
    [SerializeField] private TileBase oreFrontTile;      // 하위 호환용 유지
    [SerializeField] private List<OreSpawnEntry> oreEntries = new List<OreSpawnEntry>();
    [SerializeField] private List<TileData> mineableTileList = new List<TileData>();

    [Header("2.5D Core Keeper Style Settings")]
    [SerializeField] private float wallTopHeightOffset = 0.45f;
    [SerializeField] private Color groundBaseTint = new Color(0.78f, 0.75f, 0.72f, 1f);
    [SerializeField] private Color wallTopTint = Color.white;

    [Header("World Settings")]
    [SerializeField] private int chunkSize = 16;
    [SerializeField] private int loadRadius = 2;
    [SerializeField] private float noiseScale = 0.08f;
    [SerializeField] private int seed = 12345;

    private Tilemap groundLayer;
    private Tilemap shadowLayer;
    private Tilemap wallFrontLayer;
    private Tilemap wallTopLayer;

    private HashSet<Vector2Int> loadedChunks = new HashSet<Vector2Int>();
    private Dictionary<TileBase, TileData> tileDataLookup = new Dictionary<TileBase, TileData>();

    private Dictionary<TileBase, TileBase> oreFrontLookup = new Dictionary<TileBase, TileBase>();
    private Dictionary<Vector2Int, TileBase> modifiedWalls = new Dictionary<Vector2Int, TileBase>();
    private Dictionary<Vector2Int, int> currentTileHealth = new Dictionary<Vector2Int, int>();

    private TileBase runtimeWallFrontTile;
    private TileBase runtimeOreFrontTile;
    private TileBase runtimeHardShadowTile;
    private TileBase runtimeInteriorColliderTile;

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

        BuildProceduralPixelTiles();
        InitializeSharedLayers();
    }

    private void InitializeSharedLayers()
    {
        Transform parent = gridParent != null ? gridParent : transform;
        groundLayer = GetOrCreateLayer(parent, "GroundTilemap", 0, 0.5f, false);
        groundLayer.color = groundBaseTint; // 바닥 레이어 전체에 한 번만 색상 적용

        shadowLayer = GetOrCreateLayer(parent, "ShadowTilemap", 1, 0.5f, false);
        wallFrontLayer = GetOrCreateLayer(parent, "WallFrontTilemap", 2, 0.5f, true);
        wallTopLayer = GetOrCreateLayer(parent, "WallTopTilemap", 5, 0.5f + wallTopHeightOffset, false);
    }

    private Tilemap GetOrCreateLayer(Transform parent, string layerName, int sortingOrder, float anchorY, bool withCollider)
    {
        Transform tr = parent.Find(layerName);
        GameObject go = tr != null ? tr.gameObject : new GameObject(layerName);
        if (tr == null) go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;

        Tilemap tm = go.GetComponent<Tilemap>();
        if (tm == null) tm = go.AddComponent<Tilemap>();
        tm.tileAnchor = new Vector3(0.5f, anchorY, 0f);

        TilemapRenderer tmr = go.GetComponent<TilemapRenderer>();
        if (tmr == null) tmr = go.AddComponent<TilemapRenderer>();
        tmr.sortingOrder = sortingOrder;
        tmr.mode = (sortingOrder == 0 || sortingOrder == 5)
            ? TilemapRenderer.Mode.Individual
            : TilemapRenderer.Mode.Chunk;

        if (withCollider)
        {
            Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
            if (rb == null) rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;

            TilemapCollider2D tmc = go.GetComponent<TilemapCollider2D>();
            if (tmc == null) tmc = go.AddComponent<TilemapCollider2D>();
            tmc.compositeOperation = Collider2D.CompositeOperation.Merge;

            CompositeCollider2D cc = go.GetComponent<CompositeCollider2D>();
            if (cc == null) cc = go.AddComponent<CompositeCollider2D>();
            cc.geometryType = CompositeCollider2D.GeometryType.Outlines;
            cc.edgeRadius = 0.02f;
        }

        return tm;
    }

    private void BuildProceduralPixelTiles()
    {
        runtimeHardShadowTile = CreateHardEdgeShadowTile(3);
        runtimeInteriorColliderTile = CreateInvisibleColliderTile();

        runtimeWallFrontTile = wallFrontTile != null
            ? wallFrontTile
            : CreateStratifiedFrontTile(
                new Color32(118, 122, 130, 255),
                new Color32(88, 92, 100, 255),
                new Color32(64, 67, 74, 255),
                new Color32(28, 30, 35, 255),
                false,
                default
            );

        runtimeOreFrontTile = oreFrontTile != null
            ? oreFrontTile
            : CreateStratifiedFrontTile(
                new Color32(124, 118, 112, 255),
                new Color32(92, 86, 82, 255),
                new Color32(66, 62, 60, 255),
                new Color32(28, 26, 28, 255),
                true,
                new Color32(218, 145, 72, 255)
            );

        oreFrontLookup.Clear();
        foreach (OreSpawnEntry entry in oreEntries)
        {
            if (entry.oreTopTile == null || oreFrontLookup.ContainsKey(entry.oreTopTile)) continue;

            TileBase front = entry.oreFrontTile != null
                ? entry.oreFrontTile
                : CreateStratifiedFrontTile(
                    new Color32(124, 118, 112, 255),
                    new Color32(92, 86, 82, 255),
                    new Color32(66, 62, 60, 255),
                    new Color32(28, 26, 28, 255),
                    true,
                    entry.oreSpeckColor
                );

            oreFrontLookup.Add(entry.oreTopTile, front);
        }
    }

    private Tile CreateInvisibleColliderTile()
    {
        Texture2D tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.SetPixels32(new Color32[16 * 16]);
        tex.Apply();

        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
        Tile tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        tile.colliderType = Tile.ColliderType.Grid;
        return tile;
    }

    private Tile CreateHardEdgeShadowTile(int shadowPixelHeight)
    {
        Texture2D tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 shadow = new Color32(16, 14, 22, 175);

        for (int y = 0; y < 16; y++)
        {
            bool isShadowRow = y >= (16 - shadowPixelHeight);
            for (int x = 0; x < 16; x++)
            {
                tex.SetPixel(x, y, isShadowRow ? shadow : clear);
            }
        }
        tex.Apply();

        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
        Tile tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        tile.colliderType = Tile.ColliderType.None;
        return tile;
    }

    private Tile CreateStratifiedFrontTile(Color32 cLight, Color32 cMid, Color32 cDark, Color32 cOutline, bool hasOreSpecks, Color32 cOre)
    {
        Texture2D tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                if (y == 0) tex.SetPixel(x, y, cOutline);
                else if (y == 7) tex.SetPixel(x, y, cLight);
                else if (y == 4 && (x % 5 != 0)) tex.SetPixel(x, y, cDark);
                else if (hasOreSpecks && ((x == 4 && y == 3) || (x == 5 && y == 3) || (x == 11 && y == 5) || (x == 12 && y == 5)))
                    tex.SetPixel(x, y, cOre);
                else tex.SetPixel(x, y, ((x + y * 3) % 7 == 0) ? cDark : cMid);
            }
        }
        tex.Apply();

        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
        Tile tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        tile.colliderType = Tile.ColliderType.Grid;
        return tile;
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

                if (!loadedChunks.Contains(coord))
                {
                    LoadChunk(coord);
                }
            }
        }

        List<Vector2Int> toUnload = new List<Vector2Int>();
        foreach (Vector2Int coord in loadedChunks)
        {
            if (!neededChunks.Contains(coord))
            {
                toUnload.Add(coord);
            }
        }

        foreach (Vector2Int coord in toUnload)
        {
            UnloadChunk(coord);
        }
    }

    private void LoadChunk(Vector2Int chunkCoord)
    {
        int startX = chunkCoord.x * chunkSize;
        int startY = chunkCoord.y * chunkSize;
        BoundsInt bounds = new BoundsInt(startX, startY, 0, chunkSize, chunkSize, 1);
        int total = chunkSize * chunkSize;

        TileBase[] groundTiles = new TileBase[total];
        TileBase[] shadowTiles = new TileBase[total];
        TileBase[] frontTiles = new TileBase[total];
        TileBase[] topTiles = new TileBase[total];

        for (int y = 0; y < chunkSize; y++)
        {
            for (int x = 0; x < chunkSize; x++)
            {
                int idx = y * chunkSize + x;
                int globalX = startX + x;
                int globalY = startY + y;
                Vector2Int globalPos = new Vector2Int(globalX, globalY);

                groundTiles[idx] = groundTile;

                TileBase currentWall = null;
                if (modifiedWalls.TryGetValue(globalPos, out TileBase savedWall))
                {
                    currentWall = savedWall;
                }
                else if (IsWallAt(globalX, globalY))
                {
                    currentWall = GetDefaultWallTileAt(globalX, globalY);
                }

                topTiles[idx] = currentWall;

                if (currentWall != null)
                {
                    if (!IsWallAt(globalX, globalY - 1))
                    {
                        frontTiles[idx] = (currentWall == oreTile) ? runtimeOreFrontTile : runtimeWallFrontTile;
                    }
                    else
                    {
                        frontTiles[idx] = runtimeInteriorColliderTile;
                    }
                }
                else if (IsWallAt(globalX, globalY + 1))
                {
                    shadowTiles[idx] = runtimeHardShadowTile;
                }
            }
        }

        groundLayer.SetTilesBlock(bounds, groundTiles);
        shadowLayer.SetTilesBlock(bounds, shadowTiles);
        wallFrontLayer.SetTilesBlock(bounds, frontTiles);
        wallTopLayer.SetTilesBlock(bounds, topTiles);

        for (int y = 0; y < chunkSize; y++)
        {
            for (int x = 0; x < chunkSize; x++)
            {
                if (topTiles[y * chunkSize + x] != null)
                {
                    Vector3Int cellPos = new Vector3Int(startX + x, startY + y, 0);
                    wallTopLayer.SetTileFlags(cellPos, TileFlags.None);
                    wallTopLayer.SetColor(cellPos, wallTopTint);
                }
            }
        }

        loadedChunks.Add(chunkCoord);
        RefreshChunkBorderRules(chunkCoord);
    }

    private void UnloadChunk(Vector2Int chunkCoord)
    {
        int startX = chunkCoord.x * chunkSize;
        int startY = chunkCoord.y * chunkSize;
        BoundsInt bounds = new BoundsInt(startX, startY, 0, chunkSize, chunkSize, 1);
        TileBase[] emptyTiles = new TileBase[chunkSize * chunkSize];

        groundLayer.SetTilesBlock(bounds, emptyTiles);
        shadowLayer.SetTilesBlock(bounds, emptyTiles);
        wallFrontLayer.SetTilesBlock(bounds, emptyTiles);
        wallTopLayer.SetTilesBlock(bounds, emptyTiles);

        loadedChunks.Remove(chunkCoord);
    }

    // 인접 청크와 맞닿는 1칸 테두리의 RuleTile 연결을 즉시 갱신
    private void RefreshChunkBorderRules(Vector2Int chunkCoord)
    {
        int startX = chunkCoord.x * chunkSize;
        int startY = chunkCoord.y * chunkSize;

        for (int i = -1; i <= chunkSize; i++)
        {
            wallTopLayer.RefreshTile(new Vector3Int(startX + i, startY - 1, 0));
            wallTopLayer.RefreshTile(new Vector3Int(startX + i, startY, 0));
            wallTopLayer.RefreshTile(new Vector3Int(startX + i, startY + chunkSize - 1, 0));
            wallTopLayer.RefreshTile(new Vector3Int(startX + i, startY + chunkSize, 0));

            wallTopLayer.RefreshTile(new Vector3Int(startX - 1, startY + i, 0));
            wallTopLayer.RefreshTile(new Vector3Int(startX, startY + i, 0));
            wallTopLayer.RefreshTile(new Vector3Int(startX + chunkSize - 1, startY + i, 0));
            wallTopLayer.RefreshTile(new Vector3Int(startX + chunkSize, startY + i, 0));
        }
    }

    private bool IsWallAt(int globalX, int globalY)
    {
        Vector2Int pos = new Vector2Int(globalX, globalY);
        if (modifiedWalls.TryGetValue(pos, out TileBase saved))
            return saved != null;

        if (Mathf.Abs(globalX) <= 2 && Mathf.Abs(globalY) <= 2)
            return false;

        float wallNoise = Mathf.PerlinNoise((globalX + seed) * noiseScale, (globalY + seed) * noiseScale);
        return wallNoise > 0.45f;
    }

    private TileBase GetFrontTileForWall(TileBase topTile)
    {
        if (topTile != null && oreFrontLookup.TryGetValue(topTile, out TileBase customOreFront))
            return customOreFront;

        return (topTile == oreTile && oreTile != null) ? runtimeOreFrontTile : runtimeWallFrontTile;
    }

    private TileBase GetDefaultWallTileAt(int globalX, int globalY)
    {
        if (oreEntries != null && oreEntries.Count > 0)
        {
            float distFromOrigin = Mathf.Sqrt(globalX * globalX + globalY * globalY);

            // 리스트 아래쪽(희귀 광석)부터 먼저 검사
            for (int i = oreEntries.Count - 1; i >= 0; i--)
            {
                OreSpawnEntry entry = oreEntries[i];
                if (entry.oreTopTile == null || distFromOrigin < entry.minDistanceFromOrigin) continue;

                float oreNoise = Mathf.PerlinNoise(
                    (globalX + seed + entry.noiseOffset) * 0.15f,
                    (globalY + seed + entry.noiseOffset) * 0.15f
                );

                if (oreNoise > entry.noiseThreshold)
                {
                    return entry.oreTopTile;
                }
            }
            return wallTile;
        }

        float fallbackNoise = Mathf.PerlinNoise((globalX + seed + 500) * 0.15f, (globalY + seed + 500) * 0.15f);
        return (oreTile != null && fallbackNoise > 0.72f) ? oreTile : wallTile;
    }

    public bool HasMineableTile(Vector2Int globalTilePos)
    {
        Vector3Int cellPos = new Vector3Int(globalTilePos.x, globalTilePos.y, 0);
        TileBase tile = wallTopLayer.GetTile(cellPos);
        return tile != null && tileDataLookup.ContainsKey(tile);
    }

    public bool DamageTile(Vector2Int globalTilePos, int damage)
    {
        Vector3Int cellPos = new Vector3Int(globalTilePos.x, globalTilePos.y, 0);
        TileBase currentTile = wallTopLayer.GetTile(cellPos);
        if (currentTile == null || !tileDataLookup.TryGetValue(currentTile, out TileData data))
            return false;

        if (damage < data.requiredMiningPower)
            return false;

        if (!currentTileHealth.TryGetValue(globalTilePos, out int hp))
        {
            hp = data.maxHealth;
        }

        hp -= damage;

        if (hp > 0)
        {
            currentTileHealth[globalTilePos] = hp;
            float ratio = (float)hp / data.maxHealth;

            wallTopLayer.SetTileFlags(cellPos, TileFlags.None);
            wallTopLayer.SetColor(cellPos, Color.Lerp(wallTopTint * 0.45f, wallTopTint, ratio));

            wallFrontLayer.SetTileFlags(cellPos, TileFlags.None);
            wallFrontLayer.SetColor(cellPos, Color.Lerp(Color.gray, Color.white, ratio));
        }
        else
        {
            currentTileHealth.Remove(globalTilePos);
            modifiedWalls[globalTilePos] = null;

            wallTopLayer.SetTileFlags(cellPos, TileFlags.None);
            wallTopLayer.SetColor(cellPos, wallTopTint);
            wallTopLayer.SetTile(cellPos, null);

            wallFrontLayer.SetTileFlags(cellPos, TileFlags.None);
            wallFrontLayer.SetColor(cellPos, Color.white);
            wallFrontLayer.SetTile(cellPos, null);

            if (wallFrontLayer.TryGetComponent(out CompositeCollider2D composite))
            {
                composite.GenerateGeometry();
            }

            SpawnDroppedItem(globalTilePos, data);

            RefreshWallVisualsAt(globalTilePos + Vector2Int.up);
            RefreshWallVisualsAt(globalTilePos);
            RefreshWallVisualsAt(globalTilePos + Vector2Int.down);

            // 부서진 칸 주변 3x3 RuleTile 테두리 즉시 재계산
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    wallTopLayer.RefreshTile(new Vector3Int(globalTilePos.x + dx, globalTilePos.y + dy, 0));
                }
            }
        }

        return true;
    }

    private void RefreshWallVisualsAt(Vector2Int globalPos)
    {
        Vector3Int cellPos = new Vector3Int(globalPos.x, globalPos.y, 0);
        TileBase top = wallTopLayer.GetTile(cellPos);

        if (top != null)
        {
            bool exposedSouth = !IsWallAt(globalPos.x, globalPos.y - 1);
            TileBase front = exposedSouth
                ? (top == oreTile ? runtimeOreFrontTile : runtimeWallFrontTile)
                : runtimeInteriorColliderTile;
            wallFrontLayer.SetTile(cellPos, front);
            shadowLayer.SetTile(cellPos, null);
        }
        else
        {
            wallFrontLayer.SetTile(cellPos, null);
            bool hasNorthWall = IsWallAt(globalPos.x, globalPos.y + 1);
            shadowLayer.SetTile(cellPos, hasNorthWall ? runtimeHardShadowTile : null);
        }

        if (wallFrontLayer.TryGetComponent(out CompositeCollider2D composite))
        {
            composite.GenerateGeometry();
        }
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