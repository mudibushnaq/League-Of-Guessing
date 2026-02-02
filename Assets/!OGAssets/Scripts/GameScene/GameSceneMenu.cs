using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement;
using I2.Loc;

public class GameSceneMenu : MonoBehaviour {
	
	public GameObject IPBoostIcon;
	public Text IPPointText;
	public Text KeysText;
	public Text PlayerName; 
	
	int AddCurrentFirst;
	
	[HideInInspector]public int CurrentIP;
	[HideInInspector]public int CurrentKeys;
	
	public GameObject NotificationBar;
	int CurrentLevel;
	public Image PlayerIcon;
	public AudioClip TypeSound;
	public AudioClip ShopEffect;
	public Sprite[] BorderRanks;
	public GameObject ShopTIP;

	public Text ProfileName;
	public Text ProfileMMR;
	public Text ProfileQurrentLevel;
	public Text CompletedGameText;
	public Image ProfileBackGround;
	public Image ProfileIcon;
	public Image ProfileRank;
	public Sprite[] RankIcon;

	// Use this for initialization
	void Start () {
		int iconIndex = PlayerPrefs.GetInt ("CurrentIcon");
		PlayerIcon.sprite = (Sprite)Resources.Load ("Icons/" + (iconIndex > 0 ? iconIndex : 1), typeof(Sprite));
		PlayerName.text = PlayerPrefs.GetString (PPrefsConstants.PlayerName);
		RefreshPoints();
	}

	public void OpenLOSPage(){
	}
	
	public void RefreshPoints(){
		CurrentIP = PlayerPrefs.GetInt ("CurrentIP");
		CurrentKeys = PlayerPrefs.GetInt ("CurrentKeys");
		IPPointText.text = CurrentIP.ToString();
		KeysText.text = CurrentKeys.ToString();
	}

	public void LoadData(){
		CurrentKeys = PlayerPrefs.GetInt ("CurrentKeys");
		PlayerPrefs.SetInt ("CurrentLevel",PlayerPrefs.GetInt("Currentlevel"));
		PlayerPrefs.SetInt("ADCounter",PlayerPrefs.GetInt("ADCounter"));
	}

	public void TypingSound(){
		//AudioManager.Instance.PlayEffect(TypeSound);
	}

	public void BackBtnClick(){
		//PlayerPrefs.SetFloat("CurrentCounter",FreeCoinsAd.StartCounter - GamePlayNEW.NextCounterFloat);
		SceneManager.LoadScene ("MainMenu");
	}

	public void ShopTriggerON(){
		ShopTIP.SetActive (true);
	}
	public void ShopTriggerOFF(){
		ShopTIP.SetActive (false);
	}

}
