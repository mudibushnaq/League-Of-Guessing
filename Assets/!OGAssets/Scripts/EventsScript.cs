using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using I2.Loc;
public class EventsScript : MonoBehaviour
{

	public MainMenuScript MMS;
	public GameObject EventBoxOBJ;
	public GameObject FBLikeOBJ;
	public GameObject FollowTwitchOBJ;
	public GameObject FBLikeWindow;

	int RandomNum;
	int BoxUsed;
	int ClientEventVersion;
	int ServerEventVersion;

	int PlayerEventAllow;
	int FBEventGiven;

	int isFBEventLike;
	int FollowedTwitch;

	void Start ()
	{
		ClientEventVersion = 1;
		BoxUsed = PlayerPrefs.GetInt ("BoxUsedSTR");
		isFBEventLike = PlayerPrefs.GetInt ("FBLikeInt");
		if (isFBEventLike == 0) {
			FBLikeOBJ.SetActive (true);
		} else {
			FBLikeOBJ.SetActive (false);
		}

		FollowedTwitch = PlayerPrefs.GetInt ("TwitchFollowed");
		if (FollowedTwitch == 0) {
			FollowTwitchOBJ.SetActive (true);
		} else {
			FollowTwitchOBJ.SetActive (false);
		}

		EventBoxOBJ.SetActive (false);
		//StartCoroutine (StartCR (0.5F));
	}

	public void EventBoxBtn ()
	{
		RandomNum = Random.Range (5, 20);
		PlayerPrefs.SetInt ("BoxUsedSTR", 1);
		EventBoxOBJ.SetActive (true);
		ShopManager.ShowMessage ("You've been rewarded with " + RandomNum + " Points");
		PlayerPrefs.SetInt ("CurrentIP", PlayerPrefs.GetInt ("CurrentIP") + RandomNum);

		MMS.RefreshPoints ();
		EventBoxOBJ.GetComponent<Button> ().interactable = false;
		EventBoxOBJ.SetActive (false);
		reqPlayerCollectedRandIP ();
	}

	public void FollowTwitchBtn ()
	{
		PlayerPrefs.SetInt ("TwitchFollowed", 1);
		StartCoroutine (showFBLikeCollectWindow ());

		FollowTwitchOBJ.SetActive (false);
		Application.OpenURL ("https://www.twitch.tv/projectlucianeu/");

		reqPlayerFollowedTwitch ();
	}

	public void FBLikeEventClick ()
	{
		PlayerPrefs.SetInt ("FBLikeInt", 1);
		FBLikeOBJ.SetActive (false);
		StartCoroutine (showFBLikeCollectWindow ());

#if UNITY_ANDROID
		OpenFacebookPage ();
#elif UNITY_IOS
		if(URLSchemeSupport.isURLSchemeSupported("fb://")){
			Application.OpenURL("fb://profile/992459800787688");
		}else{
			Application.OpenURL("https://www.facebook.com/leagueofguessing");
		}
#endif
		reqPlayerLikedFB ();

	}

	void OpenFacebookPage ()
	{
		WWW www = new WWW ("fb://page/992459800787688");
		StartCoroutine (WaitForRequest (www));
	}

	IEnumerator WaitForRequest (WWW www)
	{
		yield return www;

		if (www.error == null) {
			Debug.Log ("Sucess!: " + www.text);
		} else {
			Debug.Log ("WWW Error: " + www.error + " Opening Safari...");
			Application.OpenURL ("https://www.facebook.com/leagueofguessing");

		}    
	}

	private void reqPlayerLikedFB ()
	{
		Debug.Log ("Requesting PlayerLikedFB");
		//NewAnalytics.Instance.LogEvent ("PlayerLikedFB");
	}

	private void reqPlayerCollectedRandIP ()
	{
		Debug.Log ("Requesting PlayerCollectedRandIP");
		//NewAnalytics.Instance.LogEvent ("PlayerCollectedRandIP");
	}

	private void reqPlayerFollowedTwitch ()
	{
		Debug.Log ("Requesting PlayerFollowedTwitch");
		//NewAnalytics.Instance.LogEvent ("PlayerFollowedTwitch");
	}

	IEnumerator showFBLikeCollectWindow ()
	{
		yield return new WaitForSeconds (2);
		FBLikeWindow.SetActive (true);
	}

	public void FBLikeCollect ()
	{
		PlayerPrefs.SetInt ("CurrentKeys", PlayerPrefs.GetInt ("CurrentKeys") + 5);
		MMS.RefreshPoints ();
		FBLikeWindow.SetActive (false);
		ShopManager.ShowMessage ("You've been rewarded with " + 5 + " Keys for liking our Facebook Page.");
	}
}
