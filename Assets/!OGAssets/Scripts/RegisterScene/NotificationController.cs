using UnityEngine;
using System;
using System.Collections;
using UnityEngine.UI;
using DG.Tweening;
using I2.Loc;


public class NotificationController : MonoBehaviour
{

	public GameObject NotificationBar;
	public Text NotificationText;

    public static NotificationController Instance;

	// Use this for initialization
	void Start ()
	{
        Instance = this;
        //DontDestroyOnLoad(this);
        NotificationBar.SetActive (false);
	}

	public void ShowNotification (string term)
	{
		//NotificationText.text = ScriptLocalization.Get (term);
		NotificationBar.SetActive (true);
	}

	public void ShowTransiantNotification (string term, float timeSec = 2f, Action completed = null)
	{
		ShowTransiantNotification (term, "", timeSec, completed);
	}

	public void ShowTransiantNotification (string term, string message, float timeSec = 2f, Action completed = null)
	{
		//String txt = ScriptLocalization.Get (term) + message;
		//NotificationText.text = txt;
		NotificationBar.SetActive (true);
		StartCoroutine (hideAfterDelay (timeSec, completed));
	}

	public void HideNotification ()
	{
		NotificationText.text = "";
		NotificationBar.SetActive (false);
	}

	IEnumerator hideAfterDelay (float time, Action completed = null)
	{
		yield return new WaitForSeconds (time);
		NotificationBar.GetComponent<Image> ().DOColor (Color.clear, 0.1f);
		yield return new WaitForSeconds (0.1f);
		NotificationBar.SetActive (false);
		NotificationBar.GetComponent<Image> ().color = Color.white;
		if (completed != null) {
			completed ();
		}
	}
}
