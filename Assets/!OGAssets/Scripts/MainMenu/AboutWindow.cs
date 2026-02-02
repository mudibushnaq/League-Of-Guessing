using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.UI;

public class AboutWindow : MonoBehaviour
{

	public Text VersionText;

	void Start ()
	{
	}

	public void TwitterClick ()
	{
		Application.OpenURL ("https://twitter.com/bestsejueu");
	}

	public void YoutubeClick ()
	{
		Application.OpenURL ("https://www.youtube.com/channel/UCxu59YVIekbt6ZsLMeEBAig");
	}

	public void FacebookClick ()
	{
		Application.OpenURL ("https://www.facebook.com/leagueofguessing");
	}

	public void TwitchClick ()
	{
		Application.OpenURL ("https://twitch.tv/bestsejueu");
	}

	public void PaypalClick ()
	{
		Application.OpenURL ("https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=3T2NQTDQ44MN6");
	}

	public void OKButtonClick ()
	{
		this.gameObject.SetActive (false);

	}

	public void OPENButtonClick ()
	{
		this.gameObject.SetActive (true);
	}

	public void RestartGame ()
	{
		SceneManager.LoadScene ("MainMenu");
		PlayerPrefs.SetInt ("MyFirstUp", 0);
		PlayerPrefs.SetInt ("CurrentData", 0);
		PlayerPrefs.SetInt ("QUnlocked", 0);
		PlayerPrefs.SetInt ("WUnlocked", 0);
		PlayerPrefs.SetInt ("EUnlocked", 0);
		PlayerPrefs.SetInt ("RUnlocked", 0);
		PlayerPrefs.SetInt ("UnlockedSkills", 0);
		PlayerPrefs.SetInt ("UnlockRandomSkill", 0);
		PlayerPrefs.SetInt ("GameFinished", PlayerPrefs.GetInt ("GameFinished") + 1);
	}

	public void GroupClick ()
	{
		Application.OpenURL ("https://www.facebook.com/groups/374955082701147/");
	}
}
