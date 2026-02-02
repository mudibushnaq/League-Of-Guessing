using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using I2.Loc;
public class ExchangeScript : MonoBehaviour
{

	public GameObject IconsPanel, KeysPanel, IPBoostPanel, ExchangePanel,IPPanel,RanksPanel;
	public GameObject ShopHint, BannerAcceptBtn, IconAcceptBtn, ProfilePanel;
	public RectTransform ContainerPannelRect;
	public Scrollbar scrollbar;
	MainMenuScript MMS;
	int MMR;
	string SelectedIcon;
	int SelectedBanner;
	string ButtonPressed;
	public int BuyKey1, BuyKey2, BuyKey3, BuyKey4;
	public int IPCost1, IPCost2, IPCost3, IPCost4;
	IconsScriptSHOP iconsscripts;
	public GameObject GlowEffect;
	public GameObject profilePurchasedEffect;
	//public GameObject ValBG1, ValBG2, ValBG3, Event2BG1, Event2BG2, Event2BG3;
	//public GameObject ValIcon1, ValIcon2, ValIcon3, Event2Icon1, Event2Icon2, Event2Icon3;
	string BannersIntsSaved;
	string[] ProfileBannersUnlocked;
	public List<string> ProfileBannersUnlockedNEW;

	//public GameObject IconsPanel2;
	//public GameObject Page2IconsBtnUnlock;

	void Start ()
	{
		MMS = GetComponent<MainMenuScript> ();
		iconsscripts = GetComponent<IconsScriptSHOP> ();
		ShopHint.SetActive (false);
		BannerAcceptBtn.SetActive (false);
		IconAcceptBtn.SetActive (false);
		initializeIconsListeners ();
		//bool isIconsPage2Available = PlayerPrefs.GetInt (Constants.PAGE2_ICONS_AVAILABILTY) == 1;
		/*if (isIconsPage2Available) {
			Page2IconsBtnUnlock.GetComponent<Image> ().enabled = false;
			Page2IconsBtnUnlock.GetComponentInChildren<Text>().enabled = false;
		}*/
	}

	public void IconBtnClick ()
	{
		MMS.playShopEffect ();

		//ShopHint.GetComponent<Localize> ().SetTerm ("ICONDESC_TEXT", null);
		ShopHint.SetActive (false);
		BannerAcceptBtn.SetActive (false);
		IconAcceptBtn.SetActive (true);
		IconAcceptBtn.GetComponent<Button> ().interactable = false;
		IconsPanel.SetActive (true);
		//IconsPanel2.SetActive (false);
		KeysPanel.SetActive (false);
		IPBoostPanel.SetActive (false);
		ProfilePanel.SetActive (false);
		IPPanel.SetActive (false);
		RanksPanel.SetActive (false);
		int addedRows = Mathf.CeilToInt (iconsscripts.GetAndSplitIcons () / 4.0f);
		iconsscripts.GlowEffect.SetActive (false);
		scrollbar.value = 1;
		ContainerPannelRect.offsetMin = new Vector2 (0, -1265 + addedRows * -95);

	}

	public void IconPage2BtnClick ()
	{
		MMS.playShopEffect ();

		//ShopHint.GetComponent<Localize> ().SetTerm ("ICONDESC_TEXT", null);
		ShopHint.SetActive (false);
		BannerAcceptBtn.SetActive (false);
		IconAcceptBtn.SetActive (true);
		IconAcceptBtn.GetComponent<Button> ().interactable = false;
		IconsPanel.SetActive (false);
		//IconsPanel2.SetActive (true);
		KeysPanel.SetActive (false);
		IPBoostPanel.SetActive (false);
		ProfilePanel.SetActive (false);
		IPPanel.SetActive (false);
		RanksPanel.SetActive (false);
		int addedRows = Mathf.CeilToInt (iconsscripts.GetAndSplitIcons () / 4.0f);
		iconsscripts.GlowEffect.SetActive (false);
		scrollbar.value = 1;
		ContainerPannelRect.offsetMin = new Vector2 (0, -1265 + addedRows * -95);

	}

	public void KeysBtnClick ()
	{
		MMS.playShopEffect ();
		//ShopHint.GetComponent<Localize> ().SetTerm ("KEYSDESC_TEXT", null);
		ShopHint.SetActive (true);
		BannerAcceptBtn.SetActive (false);
		IconAcceptBtn.SetActive (false);
		IconsPanel.SetActive (false);
		IPPanel.SetActive (false);
		KeysPanel.SetActive (true);
		IPBoostPanel.SetActive (false);
		ProfilePanel.SetActive (false);
		RanksPanel.SetActive (false);
		scrollbar.value = 1;
		ContainerPannelRect.offsetMin = new Vector2 (0, -238);

	}

	public void IPBtnClick ()
	{
		MMS.playShopEffect ();

		//ShopHint.GetComponent<Localize> ().SetTerm ("KEYSDESC_TEXT", null);
		ShopHint.GetComponent<Text>().text = "IP is used to buy Keys/Icons/Banners/Unlock Ranks";
		ShopHint.SetActive (true);
		BannerAcceptBtn.SetActive (false);
		IconAcceptBtn.SetActive (false);
		IconsPanel.SetActive (false);
		IPPanel.SetActive (true);
		KeysPanel.SetActive (false);
		IPBoostPanel.SetActive (false);
		ProfilePanel.SetActive (false);
		RanksPanel.SetActive (false);
		scrollbar.value = 1;
		ContainerPannelRect.offsetMin = new Vector2 (0, -238);

	}

	public void BannersBtnClick ()
	{
		MMS.playShopEffect ();
		//ShopHint.GetComponent<Localize> ().SetTerm ("BANNERSDESC_TEXT", null);
		ShopHint.SetActive (true);
		BannerAcceptBtn.SetActive (false);
		IconAcceptBtn.SetActive (false);
		IconsPanel.SetActive (false);
		KeysPanel.SetActive (false);
		IPBoostPanel.SetActive (false);
		ProfilePanel.SetActive (false);
		IPPanel.SetActive (false);
		RanksPanel.SetActive (false);
		scrollbar.value = 1;
		ContainerPannelRect.offsetMin = new Vector2 (0, -860);
	}

	public void IPBoostBtnClick ()
	{
		MMS.playShopEffect ();
		//ShopHint.GetComponent<Localize> ().SetTerm ("KEYSDESC_TEXT", null);
		ShopHint.GetComponent<Text>().text = "IP Boosters are used per game and they double (2x) the IP you gain per level";
		ShopHint.SetActive (true);
		BannerAcceptBtn.SetActive (false);
		IconAcceptBtn.SetActive (false);
		IconsPanel.SetActive (false);
		IPPanel.SetActive (false);
		KeysPanel.SetActive (false);
		ProfilePanel.SetActive (false);
		IPBoostPanel.SetActive (true);
		RanksPanel.SetActive (false);
		scrollbar.value = 1;
		ContainerPannelRect.offsetMin = new Vector2 (0, -238);
	}

	public void RanksBtnClick ()
	{
		MMS.playShopEffect ();
		//ShopHint.GetComponent<Localize> ().SetTerm ("KEYSDESC_TEXT", null);
		ShopHint.GetComponent<Text>().text = "Ranks are used to show your current level in the game , progressing in levels will unlock more ranks";
		ShopHint.SetActive (true);
		BannerAcceptBtn.SetActive (false);
		IconAcceptBtn.SetActive (false);
		IconsPanel.SetActive (false);
		IPPanel.SetActive (false);
		KeysPanel.SetActive (false);
		ProfilePanel.SetActive (false);
		IPBoostPanel.SetActive (false);
		RanksPanel.SetActive (true);
		scrollbar.value = 1;
		ContainerPannelRect.offsetMin = new Vector2 (0, -238);
	}

	public void CloseExchange ()
	{
		ShopHint.SetActive (false);
		BannerAcceptBtn.SetActive (false);
		IconAcceptBtn.SetActive (false);
		KeysPanel.SetActive (false);
		IPBoostPanel.SetActive (false);
		GlowEffect.SetActive (false);
		RanksPanel.SetActive (false);
		MMS.CloseShopWindow ();
		MMS.RefreshPoints ();
	}

	public void Exchange2Keys ()
	{
		//int currentKeys = PlayerPrefs.GetInt ("CurrentKeys");
		if (PlayerPrefs.GetInt ("CurrentIP") >= IPCost1) {
			PlayerPrefs.SetInt ("CurrentIP", PlayerPrefs.GetInt ("CurrentIP") - IPCost1);
			PlayerPrefs.SetInt ("CurrentKeys", PlayerPrefs.GetInt ("CurrentKeys") + BuyKey1);
			MMS.RefreshPoints ();
			//LogKeyPurchase (BuyKey1, currentKeys, IPCost1);
			ShopManager.ShowMessage ("You've exchanged " + BuyKey1 + " for " + IPCost1 + " IP");
            AnalyticsManager.Instance.logEventBuyEvent(PlayerPrefs.GetInt("CurrentIP"),2);

        } else {
			ShopManager.ShowMessage ("Not Enough IP");
			/*MMS.NotificationBar.SetActive (true);
			MMS.NotificationBar.transform.GetChild (0).gameObject.GetComponent<Localize> ().SetTerm ("NOMOREIP_TEXT", null);
			StartCoroutine (MMS.TurnOffNofy (0.5F));*/
		}
	}

	public void Exchange5Keys ()
	{
		//int currentKeys = PlayerPrefs.GetInt ("CurrentKeys");

		if (PlayerPrefs.GetInt ("CurrentIP") >= IPCost2) {
			PlayerPrefs.SetInt ("CurrentIP", PlayerPrefs.GetInt ("CurrentIP") - IPCost2);
			PlayerPrefs.SetInt ("CurrentKeys", PlayerPrefs.GetInt ("CurrentKeys") + BuyKey2);
			MMS.RefreshPoints ();
			//LogKeyPurchase (BuyKey2, currentKeys, IPCost2);
			ShopManager.ShowMessage ("You've exchanged " + BuyKey2 + " for " + IPCost2 + " IP");
            AnalyticsManager.Instance.logEventBuyEvent(PlayerPrefs.GetInt("CurrentIP"), 5);
        } else {
			ShopManager.ShowMessage ("Not enough IP");
			/*MMS.NotificationBar.SetActive (true);
			MMS.NotificationBar.transform.GetChild (0).gameObject.GetComponent<Localize> ().SetTerm ("NOMOREIP_TEXT", null);
			StartCoroutine (MMS.TurnOffNofy (0.5F));*/
		}
	}

	public void Exchange10Keys ()
	{
		//int currentKeys = PlayerPrefs.GetInt ("CurrentKeys");

		if (PlayerPrefs.GetInt ("CurrentIP") >= IPCost3) {
			PlayerPrefs.SetInt ("CurrentIP", PlayerPrefs.GetInt ("CurrentIP") - IPCost3);
			PlayerPrefs.SetInt ("CurrentKeys", PlayerPrefs.GetInt ("CurrentKeys") + BuyKey3);
			MMS.RefreshPoints ();
			//LogKeyPurchase (BuyKey3, currentKeys, IPCost3);
			ShopManager.ShowMessage ("You've exchanged " + BuyKey3 + " for " + IPCost3 + " IP");
            AnalyticsManager.Instance.logEventBuyEvent(PlayerPrefs.GetInt("CurrentIP"), 10);
        } else {
			ShopManager.ShowMessage ("Not enough IP");
			/*MMS.NotificationBar.SetActive (true);
			MMS.NotificationBar.transform.GetChild (0).gameObject.GetComponent<Localize> ().SetTerm ("NOMOREIP_TEXT", null);
			StartCoroutine (MMS.TurnOffNofy (0.5F));*/
		}
	}

	public void Exchange15Keys ()
	{
		//int currentKeys = PlayerPrefs.GetInt ("CurrentKeys");

		if (PlayerPrefs.GetInt ("CurrentIP") >= IPCost4) {
			PlayerPrefs.SetInt ("CurrentIP", PlayerPrefs.GetInt ("CurrentIP") - IPCost4);
			PlayerPrefs.SetInt ("CurrentKeys", PlayerPrefs.GetInt ("CurrentKeys") + BuyKey4);
			MMS.RefreshPoints ();
			//LogKeyPurchase (BuyKey4, currentKeys, IPCost4);
			ShopManager.ShowMessage ("You've exchanged " + BuyKey4 + " for " + IPCost4 + " IP");
            AnalyticsManager.Instance.logEventBuyEvent(PlayerPrefs.GetInt("CurrentIP"), 15);
        } else {
			ShopManager.ShowMessage ("Not enough IP");
			/*MMS.NotificationBar.SetActive (true);
			MMS.NotificationBar.transform.GetChild (0).gameObject.GetComponent<Localize> ().SetTerm ("NOMOREIP_TEXT", null);
			StartCoroutine (MMS.TurnOffNofy (0.5F));*/
		}
	}

	public void Exchange2IPBoost ()
	{
		if (PlayerPrefs.GetInt ("MMR") >= 70) {
			PlayerPrefs.SetInt ("MMR", PlayerPrefs.GetInt ("MMR") - 70);
			PlayerPrefs.SetInt ("BoosterCount", PlayerPrefs.GetInt ("BoosterCount") + 2);

		}
	}

	public void OnClicked (Button button)
	{
		bool isPurchased = button.gameObject.GetComponent<PurchaseInfo> ().isPurchased;
		if (isPurchased == true) {
			SelectedBanner = System.Int32.Parse (button.name);
			GlowEffect.SetActive (true);
			GlowEffect.transform.SetParent (button.transform, false);
			PlayerPrefs.SetInt ("SelectedBanner", SelectedBanner);
			BannerAcceptBtn.GetComponent<Button> ().interactable = false;
			IconAcceptBtn.SetActive (false);
			//SetPlayerBanner ();
		} else {
			SelectedBanner = System.Int32.Parse (button.name);
			GlowEffect.SetActive (true);
			GlowEffect.transform.SetParent (button.transform, false);
			BannerAcceptBtn.GetComponent<Button> ().interactable = true;
			IconAcceptBtn.SetActive (false);
		}
	}

	public void AcceptClick ()
	{
		if (PlayerPrefs.GetInt ("CurrentIP") >= 50) {
			PlayerPrefs.SetInt ("SelectedBanner", SelectedBanner);
			PlayerPrefs.SetInt ("CurrentIP", PlayerPrefs.GetInt ("CurrentIP") - 50);
			MMS.RefreshPoints ();
			//SetPlayerBanner ();
			PlayerPrefs.SetString ("ProfileBanners", PlayerPrefs.GetString ("ProfileBanners") + SelectedBanner + "/");
			ProfilePanel.SetActive (false);
			BannerAcceptBtn.SetActive (false);
			IconAcceptBtn.GetComponent<Button> ().interactable = false;
			ShopHint.SetActive (false);
			GlowEffect.SetActive (false);
			MMS.OpenProfileWindow ();
			//AddToUnlockedBanners ();
			//Dictionary<string, object> prop = new Dictionary<string, object> () {
			//	{ "UserIP" ,PlayerPrefs.GetInt ("CurrentIP") + 50 },
			//	{ "IP" , 50 },
			//	{ "BGid" ,SelectedBanner }
			//};
			//NewAnalytics.Instance.LogEvent ("iptobgs", prop);
			ShopManager.ShowMessage ("Successfully purchased new banner");

		} else {
			ShopManager.ShowMessage ("Not enough IP");
			/*MMS.NotificationBar.SetActive (true);
			//MMS.NotificationText.text = "Changing your Banner requires 50 IP.";
			MMS.NotificationBar.transform.GetChild (0).gameObject.GetComponent<Localize> ().SetTerm ("NOMOREIP_TEXT", null);
			StartCoroutine (MMS.TurnOffNofy (0.5F));*/
		}
	}

	/*private void AddToUnlockedBanners ()
	{
		NewAnalytics.Instance.LogEvent ("SetUnlockedBanners");
	}*/

	/*private void SetPlayerBanner ()
	{
		NewAnalytics.Instance.LogEvent ("SetPlayerBanner");
	}*/

	int CheckSavedBanners ()
	{
		int count = 0;
		if (PlayerPrefs.HasKey ("ProfileBanners")) {
			BannersIntsSaved = PlayerPrefs.GetString ("ProfileBanners");
			ProfileBannersUnlocked = BannersIntsSaved.Split ('/');
			for (int i = 0; i < ProfileBannersUnlocked.Length - 1; i++) {
				ProfileBannersUnlockedNEW.Remove (ProfileBannersUnlocked [i]);
				ProfileBannersUnlockedNEW.Add (ProfileBannersUnlocked [i]);
				if (ProfileBannersUnlocked [i].Contains ("100")) {
					count++;
				}
			}

			for (int i = 0; i < ProfileBannersUnlockedNEW.Count; i++) {
				GameObject pBtn = GameObject.Find (ProfileBannersUnlockedNEW [i]);
				if (pBtn == null) {
					continue;
				} else {
					pBtn.GetComponent<PurchaseInfo> ().isPurchased = true;
					if (pBtn.transform.childCount < 2) {
						GameObject pefx = Instantiate (profilePurchasedEffect) as GameObject;
						pefx.SetActive (true);
						pefx.transform.SetParent (pBtn.transform, false);
					}
				}
			}
		}
		return count;
	}

	public void OpenProfilePanel ()
	{
		MMS.CloseProfileWindow ();
		MMS.OpenShopWindow ();
		ProfilePanel.SetActive (true);

		//ShopHint.GetComponent<Localize> ().SetTerm ("PROBANNERDESC_TEXT", null);
		ShopHint.SetActive (false);
		BannerAcceptBtn.SetActive (true);
		BannerAcceptBtn.GetComponent<Button> ().interactable = false;
		IconAcceptBtn.SetActive (false);
		IconsPanel.SetActive (false);
		KeysPanel.SetActive (false);
		IPBoostPanel.SetActive (false);
		int extraBanners = CheckSavedBanners ();
		scrollbar.value = 1;
		ContainerPannelRect.offsetMin = new Vector2 (0, (extraBanners + 12) * -170 + 440);
	}


	void initializeIconsListeners ()
	{
		IconsScriptSHOP script = GetComponent<IconsScriptSHOP> ();
		for (int i = 0; i < IconsPanel.transform.childCount; i++) {
			Transform c = IconsPanel.transform.GetChild (i);
			Button btn = c.GetComponent<Button> ();
			if (btn != null) {
				btn.onClick.AddListener (new UnityEngine.Events.UnityAction (() => script.OnClicked (btn)));
			}
		}
	}

	void OnEnable ()
	{
		//string UnlockedIcons = PlayerPrefs.GetString ("UnlockedIcons");
		//string ProfileBanners = PlayerPrefs.GetString ("ProfileBanners");
		/*
		if (ProfileBanners.Contains ("1001")) {
			ValBG1.SetActive (true);
		}
		if (ProfileBanners.Contains ("1002")) {
			ValBG2.SetActive (true);
		}
		if (ProfileBanners.Contains ("1003")) {
			ValBG3.SetActive (true);
		}
		if (ProfileBanners.Contains ("1004")) {
			Event2BG1.SetActive (true);
		}
		if (ProfileBanners.Contains ("1005")) {
			Event2BG2.SetActive (true);
		}
		if (ProfileBanners.Contains ("1006")) {
			Event2BG3.SetActive (true);
		}
		if (UnlockedIcons.Contains ("1001")) {
			ValIcon1.SetActive (true);
		}
		if (UnlockedIcons.Contains ("1002")) {
			ValIcon2.SetActive (true);
		}
		if (UnlockedIcons.Contains ("1003")) {
			ValIcon3.SetActive (true);
		}
		if (UnlockedIcons.Contains ("1004")) {
			Event2Icon1.SetActive (true);
		}
		if (UnlockedIcons.Contains ("1005")) {
			Event2Icon2.SetActive (true);
		}
		if (UnlockedIcons.Contains ("1006")) {
			Event2Icon3.SetActive (true);
		}
		*/
	}

	/*public void UnlockPage2Icons (Button btn)
	{

		if (PlayerPrefs.GetInt ("CurrentKeys") >= 2) {
			btn.gameObject.GetComponent<Image>().enabled = false;
			btn.gameObject.GetComponentInChildren<Text> ().enabled = false;
			PlayerPrefs.SetInt ("CurrentKeys", PlayerPrefs.GetInt ("CurrentKeys") - 2);
			PlayerPrefs.SetInt (Constants.PAGE2_ICONS_AVAILABILTY, 1);
			IconPage2BtnClick ();
		} else {
			Debug.Log ("Not enough IP");
		}
	}*/

}
