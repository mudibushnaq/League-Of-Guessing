using UnityEngine;
using System;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using I2.Loc;

public class MainMenuScript : MonoBehaviour
{

	public GameObject PopupGrp;
	public GameObject MainMenuGrp;
	public GameObject ExchangeWindow;
	public Button ExchangeBtn;
	public GameObject LOG3DWindow;

	public Text IPPointText;
	public Text PopupsIPPointText;
	public Text KeysText;
	public Text PopupsKeysText;
	public Text PlayerName;

	public int AddCurrentFirst;
	public static string CurrentFirst = "FirstCurrent";

	public GameObject LoadingPlayIcon;

	public AudioClip PlayEffect;
	public AudioClip ShopEffect;
	public GameObject ProfileWindow;
	//public int Today;
	//public Text FirstWinText;
	public GameObject ProfileBackGround;
	//public GameObject NotificationBar;
	public GameObject SettingsWindow;
	//public GameObject CreditsWindow;

	public Button SettingsBtn;
	public Button CreditsBtn;

	public GameObject PlayBtn;
	public GameObject ExitBtn;
	public GameObject IPBoosterIcon;
	public static bool ExchangeWindowisOpen;
	void Start ()
	{
		PlayerName.text = PlayerPrefs.GetString (PPrefsConstants.PlayerName);
		AddCurrentFirst = PlayerPrefs.GetInt ("FirstCurrent");
		//LivesIntText.text = LS.livesLeft + "X";
		//PlayerPrefs.SetInt ("CurrentIP", 100);
		if (AddCurrentFirst == 0 && PlayerPrefs.GetInt ("CurrentIP") == 0) {
			//Change When Build
			PlayerPrefs.SetInt ("CurrentIP", 10);
			PlayerPrefs.SetInt ("CurrentKeys", 5);
			PlayerPrefs.SetInt ("IPBoosterUnits", 0);
			AddCurrentFirst = 1;
			PlayerPrefs.SetInt ("CurrentLives",Constants.SKINS_MODE_LIVES);
		}
		PlayerPrefs.SetInt ("FirstCurrent", AddCurrentFirst);
		RefreshPoints ();

		//AudioManager.Instance.ReachedMenu ();

		if (PlayerPrefs.GetInt ("IPBoosterUnits") >= 1) {
			IPBoosterIcon.SetActive (true);
		}
//		Pushwoosh.ApplicationCode = "D2AE5-0E8E3";
//		Pushwoosh.GcmProjectNumber = "43110772264";
//		Pushwoosh.Instance.RegisterForPushNotifications ();

		#if UNITY_IPHONE && !UNITY_EDITOR
		ExitBtn.SetActive(false);
		#endif

		bool isOldPlayer = PlayerPrefs.GetInt ("OldPlayer") != 1 && (PlayerPrefs.GetInt (PPrefsConstants.GamesPlayed) > 0 || PlayerPrefs.GetInt ("MyFirstUp") > 0);
		if (isOldPlayer) {
			PlayerPrefs.SetInt ("OldPlayer", 0);
		} else {
			PlayerPrefs.SetInt ("OldPlayer", 1);
		}
	}

	public void RefreshPoints ()
	{
		PopupsIPPointText.text = IPPointText.text = "" + PlayerPrefs.GetInt ("CurrentIP");
		PopupsKeysText.text = KeysText.text = "" + PlayerPrefs.GetInt ("CurrentKeys");
		if (PlayerPrefs.GetInt ("IPBoosterUnits") >= 1) {
			IPBoosterIcon.SetActive (true);
		}
	}

	public void StartTheGame ()
	{
		LoadingPlayIcon.SetActive (true);
		//AudioManager.Instance.PlayEffect (PlayEffect);

        //NewAnalytics.Instance.LogEvent ("Play");

        AnalyticsManager.Instance.logPlayEvent(PlayerPrefs.GetInt("CurrentKeys"), PlayerPrefs.GetInt("CurrentIP"));

        StartCoroutine (LoadingPlayScene (3.0F));
	}

	IEnumerator LoadingPlayScene (float waitTime)
	{
		yield return new WaitForSeconds (1.0f);
		SceneManager.LoadScene ("GameScene");
	}

	public void OpenShopWindow ()
	{
		OpenPopup (ExchangeWindow);
		ExchangeWindowisOpen = true;
		GetComponent<ExchangeScript> ().IconBtnClick ();
        AnalyticsManager.Instance.logShopWindowEvent(PlayerPrefs.GetInt("CurrentKeys"), PlayerPrefs.GetInt("CurrentIP"));
    }

	public void playShopEffect ()
	{
		//AudioManager.Instance.PlayEffect (PlayEffect);
	}

	public void ExitTheGame ()
	{
		//PlayerPrefs.SetFloat ("CurrentCounter", FreeCoinsAd.StartCounter);
		#if !UNITY_STANDALONE 
		Application.Quit ();
		#endif
	}

	public void CloseShopWindow ()
	{
		closeAllPopups ();
		ExchangeWindowisOpen = false;
	}

	public void CloseSettingsWindow ()
	{
		closeAllPopups ();
	}

	public void OpenSettingsWindow ()
	{
		OpenPopup (SettingsWindow);
		SettingsBtn.interactable = false;
	}

	public void OpenCreditsWindow ()
	{
		closeAllPopups ();
		//CreditsWindow.SetActive (true);
		PopupGrp.SetActive (true);
		hideMainMenuGrp ();
		CreditsBtn.interactable = false;
	}

	public void OpenProfileWindow ()
	{
		closeAllPopups ();
		hideMainMenuGrp ();
		PopupGrp.SetActive (true);
		ProfileWindow.SetActive (true);
		if (PlayerPrefs.HasKey ("SelectedBanner")) {
			int bannerId = PlayerPrefs.GetInt ("SelectedBanner");
			if (bannerId <= 0) {
				bannerId = 13;
			}
			ProfileBackGround.GetComponent<Image> ().sprite = (Sprite)Resources.Load ("Profile/" + bannerId, typeof(Sprite));
		}
	}

	public void CloseProfileWindow ()
	{
		closeAllPopups ();
	}

	/*public IEnumerator TurnOffNofy (float waitTime)
	{
		yield return new WaitForSeconds (2.5f);
		NotificationBar.SetActive (false);
	}*/

	void closeAllPopups ()
	{
		SettingsWindow.SetActive (false);
		SettingsBtn.interactable = true;
		//CreditsWindow.SetActive (false);
		//CreditsBtn.interactable = true;

		PopupGrp.SetActive (false);
		MainMenuGrp.SetActive (true);

		ExchangeWindow.SetActive (false);
		ExchangeBtn.interactable = true;

		ProfileWindow.SetActive (false);
	}

	void hideMainMenuGrp ()
	{
		MainMenuGrp.SetActive (false);
	}

	public void ClearScreen ()
	{
		closeAllPopups ();
		hideMainMenuGrp ();
	}

	public void ShowMainMenu ()
	{
		closeAllPopups ();
	}

	private void OpenPopup (GameObject popup)
	{
		closeAllPopups ();
		hideMainMenuGrp ();
		PopupGrp.SetActive (true);
		popup.SetActive (true);
	}

	public void GoLOG3D(){
		//NewAnalytics.Instance.LogEvent ("PLAYLOG3D");
		Application.OpenURL ("https://play.google.com/store/apps/details?id=com.ObSecureGames.LOG3D");
	}

	public void CloseLOG3D(){
		LOG3DWindow.SetActive (false);
	}

	public void OpenLOG3D(){
		LOG3DWindow.SetActive (true);
	}

}
