using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;
using I2.Loc;
using System;
using UnityEngine.SceneManagement;

public class LOGLoginController : MonoBehaviour
{
	public InputField usernameField;
	
	private NotificationController notificationController;

	void Start ()
	{
		notificationController = GetComponent<NotificationController> ();
	}

	public void Login ()
	{
		notificationController.ShowNotification ("LOGGING");
		Login (usernameField.text.Trim ());
	}

	public void Login (string username)
	{
		Reset ();
		//NewAnalytics.Instance.LogEvent ("Login");

		if (username.Length < 3) {
			notificationController.ShowTransiantNotification ("NAMEERROR_TEXT", 3f);
		} else {
			//NewAnalytics.Instance.UpdatePlayerInfo (username);
			PlayerPrefs.SetString (PPrefsConstants.PlayerName, username);
			PlayerPrefs.SetInt ("CurrentNameChoose", 1);
			PlayerPrefs.SetInt ("FirstTimeDoID", 1);

			int icon = PlayerPrefs.GetInt ("CurrentIcon", -1);
			if (icon > 0) {
				ContinueLoading ();
			} else {
				// TODO the callback to set the icon then continue loading
			}
		}
	}

	private void ContinueLoading ()
	{
		notificationController.ShowNotification ("LOADING_TXT");

		SceneManager.LoadScene ("MainMenu");
	}

	public void CheckLogin ()
	{
		if (PlayerPrefs.HasKey (PPrefsConstants.PlayerName)) {
			Login (PlayerPrefs.GetString (PPrefsConstants.PlayerName, ""));
		} else {
			PlayerPrefs.SetInt ("CurrentNameChoose", 1);
			PlayerPrefs.SetInt ("FirstTimeDoID", 1);
			
		}
	}

	public void IconSelected ()
	{
		int sIcon = IconSelector.SelectedIcon;
		PlayerPrefs.SetInt ("CurrentIcon", sIcon);
        //AnalyticsManager.Instance.logLoginEvent(PlayerPrefs.GetString(PPrefsConstants.PlayerName),sIcon,AudioManager.Instance.RandomMusicNum);
        ContinueLoading ();
	}

	private void Reset ()
	{
		notificationController.HideNotification ();
		usernameField.text = "";
	}

}
