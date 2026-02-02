using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using I2.Loc;
public class IconsScriptSHOP : MonoBehaviour {

	public static int SelectedIcon;
	public GameObject GlowEffect;
	public GameObject PurchasedEffect;
	public Image PlayerIcon;
	public Image ProfileIcon;
	ExchangeScript ex;
	MainMenuScript MMS;
	string[] IconsUnlocked;
	public List<string> IconsUnlockedList;
	string IconsIntsSaved;
	void Start(){
		ex = GetComponent<ExchangeScript>();
		MMS = GetComponent<MainMenuScript>();
		SelectedIcon = PlayerPrefs.GetInt ("CurrentIcon");
		if (SelectedIcon > 0) {
			PlayerIcon.sprite = (Sprite)Resources.Load ("Icons/" + SelectedIcon, typeof(Sprite));
			ProfileIcon.sprite = PlayerIcon.sprite;
		} else {
			return;
		}
	}

	public int GetAndSplitIcons(){
		int res = 0;
		if (PlayerPrefs.HasKey ("UnlockedIcons")) {
			IconsIntsSaved = PlayerPrefs.GetString ("UnlockedIcons");
			IconsUnlocked = IconsIntsSaved.Split ('/');
			for (int i = 0; i < IconsUnlocked.Length - 1; i++) {
				IconsUnlockedList.Remove(IconsUnlocked [i]);
				IconsUnlockedList.Add (IconsUnlocked [i]);
				if (IconsUnlocked [i].Contains("100")) {
					res ++;
				}
			}
			for (int i = 0; i < IconsUnlockedList.Count; i++) {
//				Debug.Log("LOOPING "+i);
				GameObject iconBtn = GameObject.Find (IconsUnlockedList [i]);
				if(iconBtn == null){
					continue;
				}else{
					//Debug.Log("Unlocked "+i);
					iconBtn.GetComponent<PurchaseInfo>().isPurchased = true;
					if (iconBtn.transform.childCount < 2) {
						GameObject pefx = Instantiate (PurchasedEffect) as GameObject;
						pefx.SetActive (true);
						pefx.transform.SetParent (iconBtn.transform, false);
					}
				}
			}
		}
		return res;
	}

	public void OnClicked(Button button){
		bool isPurcahsed = button.gameObject.GetComponent<PurchaseInfo>().isPurchased;
		if (isPurcahsed) {
			SelectedIcon = System.Int32.Parse (button.name);
			GlowEffect.SetActive (true);
			GlowEffect.transform.SetParent(button.transform,false);
			PlayerPrefs.SetInt ("CurrentIcon", SelectedIcon);
			PlayerIcon.sprite = (Sprite)Resources.Load ("Icons/" + SelectedIcon, typeof(Sprite));
			ProfileIcon.sprite = PlayerIcon.sprite;
			ex.IconAcceptBtn.GetComponent<Button>().interactable=false;
			ex.BannerAcceptBtn.SetActive (false);
			SetPlayerIcon ();
		} else {
			SelectedIcon = System.Int32.Parse (button.name);
			GlowEffect.SetActive (true);
			GlowEffect.transform.SetParent(button.transform,false);
			ex.IconAcceptBtn.SetActive(true);
			ex.IconAcceptBtn.GetComponent<Button>().interactable=true;
			ex.BannerAcceptBtn.SetActive (false);
		}

	}

	public void AcceptClick(){
		if (PlayerPrefs.GetInt ("CurrentIP") >= 10) {
			PlayerIcon.sprite = (Sprite)Resources.Load ("Icons/" + SelectedIcon, typeof(Sprite));
			ProfileIcon.sprite = PlayerIcon.sprite;
			ex.IconsPanel.SetActive (false);
			ex.ShopHint.SetActive (false);
			ex.IconAcceptBtn.SetActive (false);
			PlayerPrefs.SetInt ("CurrentIP", PlayerPrefs.GetInt ("CurrentIP") - 10);
			MMS.RefreshPoints ();
			PlayerPrefs.SetString ("UnlockedIcons", PlayerPrefs.GetString ("UnlockedIcons") + SelectedIcon + "/");
			PlayerPrefs.SetInt ("CurrentIcon", SelectedIcon);
			UnlockedIcons ();
			//NewAnalytics.Instance.LogEvent("iptoicon",prop);
			ShopManager.ShowMessage ("Successfully purchased the icon");
            AnalyticsManager.Instance.logIconBuyEvent(PlayerPrefs.GetInt("CurrentKeys"), PlayerPrefs.GetInt("CurrentIP"), SelectedIcon);
		} else {
			ShopManager.ShowMessage ("Not enough ip");
			/*MMS.NotificationBar.SetActive (true);
			MMS.NotificationBar.transform.GetChild(0).gameObject.GetComponent<Localize>().SetTerm("ICONBUYDESC_TEXT",null);
			StartCoroutine (MMS.TurnOffNofy (0.5F));*/
		}
	}
	public void UnlockedIcons(){
		reqUnlockedIcons();
	}
	private void reqUnlockedIcons(){
		Debug.Log("Requesting AddIcon");
		//NewAnalytics.Instance.LogEvent("UnlockedIcon");
		
		SetPlayerIcon ();
	}

	private void SetPlayerIcon(){
	}

	public void CancelBtn(){
		GlowEffect.SetActive(false);
		this.gameObject.SetActive(false);
	}

}
