using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

namespace I2.Loc
{
	public class CheckManager : MonoBehaviour
	{

		private static string checkServerURl = "http://www.leagueofguessing.com/";

		public Text ClientVerText;

		private string ServerVersion;
		private string ServerUpComingVersion;
		public bool RemoveData;
		void Awake ()
		{
			Input.multiTouchEnabled = false;
			Application.targetFrameRate = 30;

			//PHPSaverScript.Instance.Initialize ();
			//NewAnalytics.Instance.Initialize ();
			//AudioManager.Instance.Initialize ();
            AnalyticsManager.Instance.Initialize();
			//LivesScript.Instance.Initialize();

#if UNITY_ANDROID
			checkServerURl += "bypass_Android.php";
#elif UNITY_IOS
			checkServerURl += "bypass_iOS.php";
#else
			checkServerURl += "bypass.php";
#endif
		}

		void Start ()
		{
			if (RemoveData == true) {
				PlayerPrefs.DeleteAll ();
			}
			ClientVerText.text = "v" + Constants.ClientVersion;
			//NotificationBar.transform.GetChild (0).gameObject.GetComponent<Localize> ().SetTerm ("VERSIONCHECK_TEXT", null);
            ShopManager.instance.ShowTransiantNotification("VERSIONCHECK_TEXT",0.9f);
            StartCoroutine(UpToDate(1.0F));
            //StartCoroutine (StartCR (0.5f));
            /*if (!PlayerPrefs.HasKey ("isADS")) {
				PlayerPrefs.SetString ("isADS","YES");
			}*/
        }

		public void CheckVersion ()
		{
			if (ServerVersion == Constants.ClientVersion || ServerUpComingVersion == Constants.ClientVersion) {
				StartCoroutine (UpToDate (1.0F));
			} else {
				StartCoroutine (Updater (1.0F));
			}
		}

		IEnumerator GoToStore (float waitTime)
		{
			yield return new WaitForSeconds (3.0f);
			Application.OpenURL ("market://details?id=com.HighlineStudio.LeagueOfGuessing");
		}

		IEnumerator UpToDate (float waitTime)
		{
			yield return new WaitForSeconds (1.0f);
			((LOGLoginController)GameObject.FindObjectOfType (typeof(LOGLoginController))).CheckLogin ();
		}

		IEnumerator Updater (float waitTime)
		{
			yield return new WaitForSeconds (1.0f);
            ShopManager.instance.ShowTransiantNotification("UPDATEVER_TEXT", 0.9f);
            StartCoroutine (GoToStore (3.0F));
		}
	}
}
