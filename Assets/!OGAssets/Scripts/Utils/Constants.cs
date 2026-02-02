using UnityEngine;
using System.Collections;

public class Constants
{
	public static int MPKeys = 5;

	public static string SKINS_MODE_AVAILABILTY = "SkinsUnlocked";
	public static string SKINS_MODE_SUGGESTION = "SkinsSuggested";
	public static string MULTIPLAYER_MODE_AVAILABILTY = "MultiplayerUnlocked";
	public static string MULTIPLAYER_MODE_SUGGESTION = "MultiplayerSuggested";
	public static string ITEMS_MODE_SUGGESTION = "ItemsSuggested";
	public static string PAGE2_ICONS_AVAILABILTY = "Page2IconsUnlocked";
	public static string Items_MODE_AVAILABILTY = "ItemsUnlocked";
	public static int SKINS_UNLOCK_COST = 50;
	public static int ITEMS_UNLOCK_COST = 50;

	public static int MODE_UNLOCK_ITEMS_VALUE = 8;
	public static int SKINS_MODE_LIVES = 5;

	#if UNITY_ANDROID
	public static string ClientVersion = "10.0";
	#elif UNITY_IOS
	public static string ClientVersion = "10.0";
	#else
	public static string ClientVersion = "10.0";
	#endif

}
