using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "NewTileData", menuName = "World/Tile Data")]
public class TileData : ScriptableObject
{
    public TileBase tileAsset;
    public int maxHealth = 3;
    public ItemData dropItem;       // 파괴 시 드롭할 ItemData 에셋
    public int dropAmount = 1;
}