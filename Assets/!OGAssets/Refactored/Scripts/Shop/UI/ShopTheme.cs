using UnityEngine;

[CreateAssetMenu(menuName="LoG/Shop/Theme")]
public sealed class ShopTheme : ScriptableObject
{
    public Color32 cardBg = new(0x19,0x1C,0x22,0xFF);
    public Color32 cardStroke = new(0x26,0x2B,0x33,0xFF);
    public Color32 price = new(0x6E,0xE7,0xB7,0xFF);
    public Color32 badge = new(0xF5,0x9E,0x0B,0xFF);
    public Sprite cardSprite;      // 9-slice rounded
    public Sprite badgeSprite;     // pill 9-slice
    public Sprite buttonSprite;    // rounded 14
}