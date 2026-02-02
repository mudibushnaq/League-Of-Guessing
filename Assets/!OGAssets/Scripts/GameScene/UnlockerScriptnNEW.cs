using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using I2.Loc;

public class UnlockerScriptnNEW : MonoBehaviour {

	GameObject QSkill,WSkill,ESkill,RSkill;
	GamePlayNEW GSM;
	int UnlockAmount;
	int QUnlocked,WUnlocked,EUnlocked,RUnlocked;
	int UnlockedSkills;
	[HideInInspector]public int RandomSkillNumber;
	public AudioClip UnlockEffect;
	public Sprite lockImage;
	public Sprite unlockImage;
	public GameObject[] TutArrows;
	public GameObject[] BlackEdges;
	//public int AddPriceToUnlock = 0;
	public Text QKeysText,WKeysText,EKeysText,RKeysText;

	void Start(){
		//PlayerPrefs.SetString ("TutFirst","SHOW");
		if (!PlayerPrefs.HasKey ("TutFirst")) {
			PlayerPrefs.SetString ("TutFirst","SHOW");
		}
		if (PlayerPrefs.GetString ("TutFirst") == "SHOW") {
			//Debug.Log ("SHOW TUT");
			for (int i = 0; i < BlackEdges.Length; i++) {
				BlackEdges [i].SetActive (true);
			}
			for (int i = 0; i < TutArrows.Length; i++) {
				TutArrows [i].SetActive (true);
			}
		}
		if (PlayerPrefs.GetString ("TutFirst") == "HIDE") {
			//Debug.Log ("HIDE TUT");
			for (int i = 0; i < BlackEdges.Length; i++) {
				BlackEdges [i].SetActive (false);
				TutArrows [i].SetActive (false);
			}
		}
		GSM = GetComponent<GamePlayNEW>();
		LoadData ();
	}

	public void UnlockQSkill(){
		PlayerPrefs.SetString ("TutFirst", "HIDE");
		GSM.CurrentKeys = PlayerPrefs.GetInt ("CurrentKeys");
		if (GSM.CurrentKeys >= 1) {
			//AudioManager.Instance.PlayEffect(UnlockEffect);
			UnlockAmount = 1;
			GSM.CurrentKeys -= UnlockAmount;
			//QSkill.transform.GetChild(0).gameObject.GetComponent<Animator>().Play("UnlockAnimationNEW");
			QSkill.transform.GetChild(0).gameObject.GetComponent<Image>().sprite = unlockImage;
			QUnlocked = 1;
			PlayerPrefs.SetInt ("QUnlocked", 1);
			SaveData();
			if(QUnlocked == 1){
				QSkill.transform.GetChild(0).gameObject.GetComponent<Button>().interactable = false;
				WSkill.transform.GetChild(0).gameObject.GetComponent<Button>().interactable = false;
				ESkill.transform.GetChild(0).gameObject.GetComponent<Button>().interactable = false;
				RSkill.transform.GetChild(0).gameObject.GetComponent<Button>().interactable = false;
				StartCoroutine(TurnOffSkill(0.5F));
			}
		} else {
			GSM.NoMoreKeysWindow.SetActive (true);
			/*GSM.NotificationBar.SetActive(true);
			//GSM.NotificationText.text = "You need 1 key to Unlock this skill Requires 1 Key.";
			GSM.NotificationBar.transform.GetChild(0).gameObject.GetComponent<Localize>().SetTerm("NOMOREKEYS_TEXT",null);
			StartCoroutine(TurnOffNofy(0.5F));*/

		}
        AnalyticsManager.Instance.logUnlockEvent(PlayerPrefs.GetInt("CurrentKeys"), PlayerPrefs.GetInt("CurrentIP"),"Q");
    }

	public void UnlockWSkill(){
		PlayerPrefs.SetString ("TutFirst", "HIDE");
		GSM.CurrentKeys = PlayerPrefs.GetInt ("CurrentKeys");
		if (GSM.CurrentKeys >= 1) {
			//AudioManager.Instance.PlayEffect (UnlockEffect);
			UnlockAmount = 1;
			GSM.CurrentKeys -= UnlockAmount;
			WSkill.transform.GetChild(0).gameObject.GetComponent<Image>().sprite = unlockImage;
			WUnlocked = 1;
			PlayerPrefs.SetInt ("WUnlocked", 1);
			SaveData();
			if(WUnlocked == 1){
				QSkill.transform.GetChild(0).gameObject.GetComponent<Button>().interactable = false;
				WSkill.transform.GetChild(0).gameObject.GetComponent<Button>().interactable = false;
				ESkill.transform.GetChild(0).gameObject.GetComponent<Button>().interactable = false;
				RSkill.transform.GetChild(0).gameObject.GetComponent<Button>().interactable = false;
				StartCoroutine(TurnOffSkill(0.5F));
			}
			//QSkill.SetActive(false);
		} else {
			GSM.NoMoreKeysWindow.SetActive (true);
			/*GSM.NotificationBar.SetActive(true);
			//GSM.NotificationText.text = "You need 1 key to Unlock this skill Requires 1 Key.";
			GSM.NotificationBar.transform.GetChild(0).gameObject.GetComponent<Localize>().SetTerm("NOMOREKEYS_TEXT",null);
			StartCoroutine(TurnOffNofy(0.5F));*/
		}
        AnalyticsManager.Instance.logUnlockEvent(PlayerPrefs.GetInt("CurrentKeys"), PlayerPrefs.GetInt("CurrentIP"), "W");
    }

	public void UnlockESkill(){
		PlayerPrefs.SetString ("TutFirst", "HIDE");
		GSM.CurrentKeys = PlayerPrefs.GetInt ("CurrentKeys");
		if (GSM.CurrentKeys >= 1) {
			//AudioManager.Instance.PlayEffect(UnlockEffect);

			UnlockAmount = 1;
			GSM.CurrentKeys -= UnlockAmount;
			ESkill.transform.GetChild(0).gameObject.GetComponent<Image>().sprite = unlockImage;
			EUnlocked = 1;
			PlayerPrefs.SetInt ("EUnlocked", 1);
			SaveData();
			if(EUnlocked == 1){
				QSkill.transform.GetChild(0).gameObject.GetComponent<Button>().interactable = false;
				WSkill.transform.GetChild(0).gameObject.GetComponent<Button>().interactable = false;
				ESkill.transform.GetChild(0).gameObject.GetComponent<Button>().interactable = false;
				RSkill.transform.GetChild(0).gameObject.GetComponent<Button>().interactable = false;
				StartCoroutine(TurnOffSkill(0.5F));
			}
			//QSkill.SetActive(false);
		} else {
			GSM.NoMoreKeysWindow.SetActive (true);
			/*GSM.NotificationBar.SetActive(true);
			//GSM.NotificationText.text = "You need 1 key to Unlock this skill Requires 1 Key.";
			GSM.NotificationBar.transform.GetChild(0).gameObject.GetComponent<Localize>().SetTerm("NOMOREKEYS_TEXT",null);
			StartCoroutine(TurnOffNofy(0.5F));*/
		}
        AnalyticsManager.Instance.logUnlockEvent(PlayerPrefs.GetInt("CurrentKeys"), PlayerPrefs.GetInt("CurrentIP"), "E");
    }

	public void UnlockRSkill(){
		PlayerPrefs.SetString ("TutFirst", "HIDE");
		GSM.CurrentKeys = PlayerPrefs.GetInt ("CurrentKeys");
		if (GSM.CurrentKeys >= 1) {
			//AudioManager.Instance.PlayEffect(UnlockEffect);
			UnlockAmount = 1;
			GSM.CurrentKeys -= UnlockAmount;
			RSkill.transform.GetChild(0).gameObject.GetComponent<Image>().sprite = unlockImage;
			RUnlocked = 1;
			PlayerPrefs.SetInt ("RUnlocked", 1);
			SaveData();
			if(RUnlocked == 1){
				QSkill.transform.GetChild(0).gameObject.GetComponent<Button>().interactable = false;
				WSkill.transform.GetChild(0).gameObject.GetComponent<Button>().interactable = false;
				ESkill.transform.GetChild(0).gameObject.GetComponent<Button>().interactable = false;
				RSkill.transform.GetChild(0).gameObject.GetComponent<Button>().interactable = false;
				StartCoroutine(TurnOffSkill(0.5F));
			}
			//QSkill.SetActive(false);
		} else {
			GSM.NoMoreKeysWindow.SetActive (true);
			/*GSM.NotificationBar.SetActive(true);
			//GSM.NotificationText.text = "You need 1 key to Unlock this skill Requires 1 Key.";
			GSM.NotificationBar.transform.GetChild(0).gameObject.GetComponent<Localize>().SetTerm("NOMOREKEYS_TEXT",null);
			StartCoroutine(TurnOffNofy(0.5F));*/
		}
        AnalyticsManager.Instance.logUnlockEvent(PlayerPrefs.GetInt("CurrentKeys"), PlayerPrefs.GetInt("CurrentIP"), "R");
    }


	IEnumerator TurnOffSkill(float waitTime) {
		yield return new WaitForSeconds(0.8f);
		QSkill.transform.GetChild(0).gameObject.GetComponent<Button>().interactable = true;
		WSkill.transform.GetChild(0).gameObject.GetComponent<Button>().interactable = true;
		ESkill.transform.GetChild(0).gameObject.GetComponent<Button>().interactable = true;
		RSkill.transform.GetChild(0).gameObject.GetComponent<Button>().interactable = true;
		for (int i = 0; i < TutArrows.Length; i++) {
			TutArrows [i].SetActive (false);
			BlackEdges [i].SetActive (false);
		}
		SkillChecker ();
	}

	public void SkillChecker(){
		if (QUnlocked == 1 || RandomSkillNumber == 0) {
			PlayerPrefs.SetInt("QUnlocked",1);
			QSkill.SetActive (false);
			TutArrows [0].SetActive (false);
		} else {
			QSkill.SetActive (true);
			if (PlayerPrefs.GetString ("TutFirst") == "SHOW") {
				TutArrows [0].SetActive (true);
			}
		}
		if(WUnlocked == 1 || RandomSkillNumber == 1){
			PlayerPrefs.SetInt("WUnlocked",1);
			WSkill.SetActive (false);
			TutArrows [1].SetActive (false);
		}else {
			WSkill.SetActive (true);
			if (PlayerPrefs.GetString ("TutFirst") == "SHOW") {
				TutArrows [1].SetActive (true);
			}
		}
		if(EUnlocked == 1 || RandomSkillNumber == 2){
			PlayerPrefs.SetInt("EUnlocked",1);
			ESkill.SetActive (false);
			TutArrows [2].SetActive (false);
		}else {
			ESkill.SetActive (true);
			if (PlayerPrefs.GetString ("TutFirst") == "SHOW") {
				TutArrows [2].SetActive (true);
			}
		}
		if(RUnlocked == 1 || RandomSkillNumber == 3){
			PlayerPrefs.SetInt("RUnlocked",1);
			RSkill.SetActive (false);
			TutArrows [3].SetActive (false);
		}else {
			RSkill.SetActive (true);
			if (PlayerPrefs.GetString ("TutFirst") == "SHOW") {
				TutArrows [3].SetActive (true);
			}
		}
		QSkill.transform.GetChild(0).gameObject.GetComponent<Image>().sprite = lockImage;
		WSkill.transform.GetChild(0).gameObject.GetComponent<Image>().sprite = lockImage;
		ESkill.transform.GetChild(0).gameObject.GetComponent<Image>().sprite = lockImage;
		RSkill.transform.GetChild(0).gameObject.GetComponent<Image>().sprite = lockImage;
	}

	public void LoadData(){
		RandomSkillNumber = PlayerPrefs.GetInt("UnlockedSkill");
		QSkill = GameObject.Find ("QSkill"); WSkill = GameObject.Find ("WSkill"); ESkill = GameObject.Find ("ESkill"); RSkill = GameObject.Find ("RSkill");
		PlayerPrefs.SetInt ("UnlockedSkills",PlayerPrefs.GetInt("UnlockedSkills"));
		PlayerPrefs.SetInt ("QUnlocked", PlayerPrefs.GetInt ("QUnlocked")); PlayerPrefs.SetInt ("WUnlocked", PlayerPrefs.GetInt ("WUnlocked"));
		PlayerPrefs.SetInt ("EUnlocked", PlayerPrefs.GetInt ("EUnlocked")); PlayerPrefs.SetInt ("RUnlocked", PlayerPrefs.GetInt ("RUnlocked"));
		PlayerPrefs.SetInt ("UnlockRandomSkill", PlayerPrefs.GetInt ("UnlockRandomSkill"));
		if (PlayerPrefs.GetInt ("UnlockRandomSkill") == 0) {
			RandomSkillNumber = Random.Range(0,3);
			PlayerPrefs.SetInt("UnlockedSkill",RandomSkillNumber);
			PlayerPrefs.SetInt ("UnlockRandomSkill",1);
		}
		//AddPriceToUnlock = PlayerPrefs.GetInt ("PriceToUnlock");
		UpdateUnlockCost ();
		GSM.RefreshPoints();
		LoadSkillsData ();

	}

	void SaveData(){
		PlayerPrefs.SetInt ("CurrentKeys",GSM.CurrentKeys);
		//NewAnalytics.Instance.LogEvent ("KeyChampM");
		PlayerPrefs.SetInt ("UnlockedSkills",PlayerPrefs.GetInt("UnlockedSkills") + 1);
		GSM.RefreshPoints();
		UnlockAmount = 1;
		//AddPriceToUnlock++;
		//PlayerPrefs.SetInt ("PriceToUnlock",AddPriceToUnlock);
		//Debug.Log (AddPriceToUnlock);
		UpdateUnlockCost ();
	}

	public void LoadSkillsData (){
		QUnlocked = PlayerPrefs.GetInt ("QUnlocked"); WUnlocked = PlayerPrefs.GetInt ("WUnlocked"); EUnlocked = PlayerPrefs.GetInt ("EUnlocked"); RUnlocked = PlayerPrefs.GetInt ("RUnlocked");
		SkillChecker ();
	}

	public void UpdateUnlockCost(){
		QKeysText.text = 1 + "X";
		WKeysText.text = 1 + "X";
		EKeysText.text = 1 + "X";
		RKeysText.text = 1 + "X";
	}
}

