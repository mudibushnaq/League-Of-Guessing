
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.Collections.Generic;
using I2.Loc;
using ObscureGames;

public class GamePlayNEW : MonoBehaviour
{
	public Image PlayerIcon;
	[HideInInspector]public int CurrentIP;
	[HideInInspector]public int CurrentKeys;
	public Text IPPointText;
	public Text KeysText;
	public AudioClip TypeSound;

	public GameObject NoMoreKeysWindow;

	//public GameObject NotificationBar;

	int keysToSkip = 1;

	KillSeriesManager killSeriesManager;

	public InputField PlayerAnswerText;
	
	public Image MyPictureImage;

	public static string[] MyQues = new string[10];
	public static int QCurrent = 0;
	public static int MyNext = 0;
	public int NumberOfTiles;
	public static int IPBooster = 0;
	public static bool isIPBoostON = false;

	/**
	 * Constent String 
	 * */
	string MyCurrentQ = "CurrentData";
	string MyDataBaseName = "MyData";
	public static int MyFirstUp = 0;

	//public static bool RightAnswer;
	public CorrectAnswerController WinWindow;

	//public static float NextCounterFloat;
	public static int ADCounter;
	bool ShowTheAD;
	[HideInInspector]public bool GameISFinished = false;
	public GameObject AboutWindowOBJ;
	public GameObject FinishedGameOBJDis;
	public Sprite AnswersLoadingImg;
	public GameObject IPBoosterIcon;

	public GameObject CurrentLevel;
	//GamePlayNEW GSM;
	UnlockerScriptnNEW UnlockScript;
	public AudioClip WinEffect;
	public GameObject SolveBtn;
	private int DoneButtons;
	public Button DoneButton;
	public Button BackButton;
	public Button ReplaceButton;
	public Button SkipButton;

	[HideInInspector]public string EnglishName, PortugueseName, PolishName, SpanishName, TurkishName, HungarianName, GreekName, KoreanName, ChineseName, JapaneseName;

	// Use this for initialization
	void Start ()
	{
		killSeriesManager = GameObject.FindObjectOfType<KillSeriesManager> ();
		//GSM = GetComponent<GamePlayNEW> ();
		PlayerPrefs.SetInt ("BuildItems", PlayerPrefs.GetInt ("BuildItems"));
		UnlockScript = GetComponent<UnlockerScriptnNEW> ();
		//NextCounterFloat = 0;

		QCurrent++;
		ADCounter = PlayerPrefs.GetInt ("CurrentADCounter");
		MyFirstUp = PlayerPrefs.GetInt ("MyFirstUp");
		if (MyFirstUp == 0) {
			int temp, iRand;
			int[] arr = new int[NumberOfTiles];
			for (int i = 0; i < NumberOfTiles; i++) {
				arr [i] = i;
			}

			// Rest From bundles
			for (int i = 0; i < NumberOfTiles; i++) {
				iRand = UnityEngine.Random.Range (0, NumberOfTiles);
				temp = arr [i];
				arr [i] = arr [iRand];
				arr [iRand] = temp;
			}

			for (int i = 0; i < NumberOfTiles; i++) {
				PlayerPrefs.SetInt ("MyNext" + i.ToString (), arr [i]);
			}
			PlayerPrefs.SetInt ("MyFirstUp", NumberOfTiles);
			PlayerPrefs.SetInt ("NumChamps", NumberOfTiles);


		} else {
			// Check for the last after adding Illaoi
			if (!PlayerPrefs.HasKey ("NumChamps") || PlayerPrefs.GetInt ("NumChamps") < NumberOfTiles) {
				PlayerPrefs.SetInt ("MyNext" + (NumberOfTiles - 1), (NumberOfTiles - 1));
				PlayerPrefs.SetInt ("NumChamps", NumberOfTiles);
			}
			CurrentLevel.SetActive (true);
		}

		QCurrent = PlayerPrefs.GetInt (MyCurrentQ);
		MyNext = PlayerPrefs.GetInt ("MyNext" + QCurrent.ToString ());

		QCurrent++;
		CurrentLevel.GetComponent<Text> ().text = QCurrent + "/" + NumberOfTiles;
		if (QCurrent > NumberOfTiles) {
			AboutWindowOBJ.SetActive (true);
			GameISFinished = true;
			FinishedGameOBJDis.SetActive (false);
		}

		SolveBtn.SetActive (false);

		loadLvlDetails (MyNext, false);
		int iconIndex = PlayerPrefs.GetInt ("CurrentIcon");
		PlayerIcon.sprite = (Sprite)Resources.Load ("Icons/" + (iconIndex > 0 ? iconIndex : 1), typeof(Sprite));
		//PlayerName.text = PlayerPrefs.GetString (PPrefsConstants.PlayerName);
		RefreshPoints();
		
	}

	void LateUpdate ()
	{
		//NextCounterFloat += Time.deltaTime;
		CurrentLevel.GetComponent<Text> ().text = QCurrent + "/" + NumberOfTiles;
		if (PlayerPrefs.GetInt ("IPBoosterUnits") >= 1) {
			isIPBoostON = true;
			IPBoosterIcon.SetActive (true);
		} else {
			isIPBoostON = false;
			IPBoosterIcon.SetActive (false);
		}
	}

	public void OnDoneButton ()
	{
		DoneButton.interactable = false;
		BackButton.interactable = false;
		ReplaceButton.interactable = false;

		//Get Champion Names List On All Langs
		if (PlayerAnswerText.text.ToUpper ().Equals (EnglishName) || PlayerAnswerText.text.ToUpper ().Equals (PortugueseName) || PlayerAnswerText.text.ToUpper ().Equals (PolishName)
		    || PlayerAnswerText.text.ToUpper ().Equals (SpanishName) || PlayerAnswerText.text.ToUpper ().Equals (TurkishName) || PlayerAnswerText.text.ToUpper ().Equals (HungarianName)
		    || PlayerAnswerText.text.Equals (GreekName, StringComparison.CurrentCultureIgnoreCase) || PlayerAnswerText.text.ToUpper ().Equals (KoreanName) || PlayerAnswerText.text.ToUpper ().Equals (ChineseName)
		    || PlayerAnswerText.text.ToUpper ().Equals (JapaneseName)) {

            //AnalyticsManager.Instance.logOnDoneWinEvent(PlayerPrefs.GetInt("CurrentKeys"), PlayerPrefs.GetInt("CurrentIP"), Int32.Parse(MyQues[0]), MyQues[1]);
			//EventsManager._instance.LogLevelWinEvent(Int32.Parse(MyQues[0]), PlayerPrefs.GetInt("CurrentKeys"), PlayerPrefs.GetInt("CurrentIP"), MyQues[1]);

			int seriesAddition = killSeriesManager.Answered (delegate() {
			});
			//AudioManager.Instance.PlayEffect (WinEffect);
			PlayerPrefs.SetInt (MyCurrentQ, QCurrent);

			int unlockedSkils = PlayerPrefs.GetInt ("UnlockedSkills");

			//IP
			bool isFirstWinON = PlayerPrefs.GetInt ("FirstWin") == 1;
			int IPBoostMultiplier = (isIPBoostON) ? 2 : 1;
			int FirstWinMultiplier = (isFirstWinON) ? 2 : 1;
			int IPInc = ((4 - unlockedSkils) * IPBoostMultiplier * FirstWinMultiplier) + seriesAddition;
			//Debug.Log (IPInc);


			if (isFirstWinON)
				PlayerPrefs.SetInt ("FirstWin", 0);
			if (isIPBoostON) {
				PlayerPrefs.SetInt ("IPBoosterUnits", PlayerPrefs.GetInt("IPBoosterUnits") - 1);
				CurrentIP += IPInc * 2; 
				if (PlayerPrefs.GetInt ("IPBoosterUnits") > 0) {
					ShopManager.ShowMessage ("You still have " + PlayerPrefs.GetInt ("IPBoosterUnits") + " Boosters");
				} else {
					ShopManager.ShowMessage ("You've spent all boosters, you don't have any booster");
				}
			} else {
				CurrentIP += IPInc; 
			}


			StartCoroutine (WaitForReset (2.0F));
			StartCoroutine (ResetScoreWindow (2.0F));

			PlayerAnswerText.text = "";

			if (DataReader.LenSize == QCurrent) {
				GameISFinished = true;
				AboutWindowOBJ.SetActive (true);
				FinishedGameOBJDis.SetActive (false);
			}

			if (QCurrent == NumberOfTiles - 1) {
				DisableSkip ();
			}

			if (QCurrent > NumberOfTiles) {
				GameISFinished = true;
				AboutWindowOBJ.SetActive (true);
				FinishedGameOBJDis.SetActive (false);
			}
			PlayerPrefs.SetInt ("CurrentIP", CurrentIP);
			RefreshPoints ();
            

        } else {
			killSeriesManager.WrongAnswer (delegate() {
			});
			DoneButton.interactable = true;
			BackButton.interactable = true;
			ReplaceButton.interactable = true;
            //AnalyticsManager.Instance.logOnDoneLoseEvent(PlayerPrefs.GetInt("CurrentKeys"), PlayerPrefs.GetInt("CurrentIP"), Int32.Parse(MyQues[0]), MyQues[1]);
			//EventsManager._instance.LogLevelFailEvent(Int32.Parse(MyQues[0]), PlayerPrefs.GetInt("CurrentKeys"), PlayerPrefs.GetInt("CurrentIP"), MyQues[1]);

		}

		if (PlayerPrefs.GetString ("TutFirst") == "HIDE") {
			//Debug.Log ("HIDE TUT");
			for (int i = 0; i < UnlockScript.BlackEdges.Length; i++) {
				UnlockScript.BlackEdges [i].SetActive (false);
				UnlockScript.TutArrows [i].SetActive (false);
			}
		}

        
	}

	private void DisableSkip ()
	{ 
		SkipButton.interactable = false;
	}

	private void EnableSkip ()
	{ 
		SkipButton.interactable = true;
	}

	IEnumerator LoadingSkinsScene (float waitTime)
	{
		yield return new WaitForSeconds (1.0f);
		SceneManager.LoadScene ("SkinMode");
	}

	IEnumerator LoadingItemsMode (float waitTime)
	{
		yield return new WaitForSeconds (1.0f);
		SceneManager.LoadScene ("ItemsMode");
	}

	IEnumerator ExitGame (float waitTime)
	{
		yield return new WaitForSeconds (5.0f);
		Application.Quit ();
	}

	IEnumerator ResetScoreWindow (float waitTime)
	{
		yield return new WaitForSeconds (5.0f);
	}

	IEnumerator WaitForReset (float waitTime)
	{
		yield return new WaitForSeconds (2.0f);
		MyNext = PlayerPrefs.GetInt ("MyNext" + QCurrent.ToString ());
		QCurrent++;

		loadLvlDetails (MyNext, true);

		yield return new WaitForSeconds (0.5f);
		DoneButton.interactable = true;
		BackButton.interactable = true;
		ReplaceButton.interactable = true;
		DoneButtons = 0;
		PlayerPrefs.SetInt ("PriceToUnlock",0);
	}

	public void SOLVERFORTEST ()
	{
		PlayerAnswerText.text = MyQues [1];
	}

	private void loadSkillImages (string champId, Image container, Sprite loadingImg)
	{
		//Debug.Log ("Loading Skill Form Resources");
		container.sprite = (Sprite)Resources.Load ("ChampSkills_NEW/" + champId, typeof(Sprite));
	}

	public void Skip ()
	{
		CurrentKeys = PlayerPrefs.GetInt ("CurrentKeys");
		if (PlayerPrefs.GetInt ("UnlockedSkills") < 0) {
			//NotificationBar.SetActive (true);
			//NotificationBar.transform.GetChild (0).gameObject.GetComponent<Localize> ().SetTerm (, null);
            ShopManager.instance.ShowTransiantNotification("SKIP_UNLOCK_FIRST_TEXT");
            AnalyticsManager.Instance.logSkipButtonEvent(PlayerPrefs.GetInt("CurrentKeys"), PlayerPrefs.GetInt("CurrentIP"), Int32.Parse(MyQues[0]), MyQues[1]);
            //StartCoroutine (TurnOffNotify ());
        } else {
			if (CurrentKeys >= keysToSkip) {
				bool canReplace = false;
				CurrentKeys -= keysToSkip;
				int lvlIndex = QCurrent - 1;


				int lvlChampIndex = PlayerPrefs.GetInt ("MyNext" + lvlIndex);
				int replacementLvl = 0;

				canReplace = true;
				replacementLvl = UnityEngine.Random.Range (QCurrent, NumberOfTiles);
				//Debug.Log ("--------------can replace " + canReplace);
				if (canReplace) {

					int replacemenChampIndex = PlayerPrefs.GetInt ("MyNext" + replacementLvl);

					//Debug.Log ("lvlIndex:" + lvlIndex + "   switch wirh:" + replacementLvl);

					//Debug.Log (lvlChampIndex + "   to " + replacemenChampIndex);

					PlayerPrefs.SetInt ("MyNext" + lvlIndex, replacemenChampIndex);
					PlayerPrefs.SetInt ("MyNext" + replacementLvl, lvlChampIndex);
					loadLvlDetails (replacemenChampIndex, true);
					PlayerPrefs.SetInt ("CurrentKeys", CurrentKeys);
					RefreshPoints ();
                    ShopManager.instance.ShowTransiantNotification("SKIP_MSG_TEXT");
                    //NotificationBar.SetActive (true);
                    //NotificationBar.transform.GetChild (0).gameObject.GetComponent<Localize> ().SetTerm ("SKIP_MSG_TEXT", null);

                    //StartCoroutine (TurnOffNotify ());
                } else {
                    ShopManager.instance.ShowTransiantNotification("SKIP_DOWNLOAD_NEEDED_ERR");
                    //NotificationBar.SetActive (true);
                    //NotificationBar.transform.GetChild (0).gameObject.GetComponent<Localize> ().SetTerm ("SKIP_DOWNLOAD_NEEDED_ERR", null);

                    //StartCoroutine (TurnOffNotify (3f));
                }
			} else {
				NoMoreKeysWindow.SetActive (true);
				/*NotificationBar.SetActive (true);
				NotificationBar.transform.GetChild (0).gameObject.GetComponent<Localize> ().SetTerm ("SKIP_NOMOREKEYS_TEXT", null);
				StartCoroutine (TurnOffNotify ());*/
			}
		}
	}


	private void loadLvlDetails (int ChampIndex, bool resetUnlocks)
	{
		if (resetUnlocks) {
			PlayerPrefs.SetInt ("BuildItems", 0);
			PlayerPrefs.SetInt ("QUnlocked", 0);
			PlayerPrefs.SetInt ("WUnlocked", 0);
			PlayerPrefs.SetInt ("EUnlocked", 0);
			PlayerPrefs.SetInt ("RUnlocked", 0);
			PlayerPrefs.SetInt ("UnlockRandomSkill", 0);
			PlayerPrefs.SetInt ("UnlockedSkills", 0);
			PlayerPrefs.SetInt ("PriceToUnlock",0);
			//UnlockScript.AddPriceToUnlock = PlayerPrefs.GetInt ("PriceToUnlock");
			UnlockScript.UpdateUnlockCost ();
			if (PlayerPrefs.GetInt ("UnlockRandomSkill") == 0) {
				UnlockScript.RandomSkillNumber = UnityEngine.Random.Range (0, 3);
				PlayerPrefs.SetInt ("UnlockedSkill", UnlockScript.RandomSkillNumber);
				PlayerPrefs.SetInt ("UnlockRandomSkill", 1);
			}
		}

		UnlockScript.LoadSkillsData ();

		MyQues = DataReader.GetLineStr (DataReader.GetLine (MyDataBaseName, ChampIndex));

		if (isChampSkillsSpriteAvailable (MyQues [0])) {
			loadSkillImages (MyQues [0], MyPictureImage, AnswersLoadingImg);

			EnglishName = MyQues [1];
			PortugueseName = MyQues [2];
			PolishName = MyQues [3];
			SpanishName = MyQues [4];
			TurkishName = MyQues [5];
			HungarianName = MyQues [6];
			GreekName = MyQues [7];
			KoreanName = MyQues [8];
			ChineseName = MyQues [9];
			JapaneseName = MyQues [10];
			//#if UNITY_EDITOR
			//Debug.Log ("ChampIndex " + ChampIndex.ToString ());
			//Debug.Log ("Current level is " + MyQues [0] + " Champion Answer is " + MyQues [1] + " Other Lang Champion Name " + MyQues [2]);
			//#endif
		} else {
			//Debug.Log ("Level not ready");
			PlayerPrefs.SetInt ("needDownloadNotify", 1);
			SceneManager.LoadScene ("MainMenu");
		}

		EnableSkip ();
	}

	public bool isChampSkillsSpriteAvailable (string champIndexStr)
	{
		return true;
	}

	private void AddToUnlockedBanners (int bannerID)
	{
		PlayerPrefs.SetString ("ProfileBanners", PlayerPrefs.GetString ("ProfileBanners") + bannerID + "/");
		//NewAnalytics.Instance.LogEvent ("SetUnlockedBanners");
	}

	public void RefreshPoints(){
		CurrentIP = PlayerPrefs.GetInt ("CurrentIP");
		CurrentKeys = PlayerPrefs.GetInt ("CurrentKeys");
		IPPointText.text = CurrentIP.ToString();
		KeysText.text = CurrentKeys.ToString();
	}

	public void TypingSound(){
		//AudioManager.Instance.PlayEffect(TypeSound);
	}

	public void BackBtnClick(){
        //PlayerPrefs.SetFloat("CurrentCounter",FreeCoinsAd.StartCounter - GamePlayNEW.NextCounterFloat);
        AnalyticsManager.Instance.logBackButtonEvent(PlayerPrefs.GetInt("CurrentKeys"), PlayerPrefs.GetInt("CurrentIP"), Int32.Parse(MyQues[0]),MyQues[1]);
		SceneManager.LoadScene ("MainMenu");
	}

}
