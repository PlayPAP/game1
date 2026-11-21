using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "NewTileData", menuName = "MiningGame/Tile Data")]
public class TileData : ScriptableObject
{
    public TileBase tileAsset;
    public int maxHealth = 3;
    public int requiredMiningPower = 1; // 1: 기본/나무, 2: 구리, 3: 철 곡괭이 이상
    public ItemData dropItem;
    public int dropAmount = 1;
}