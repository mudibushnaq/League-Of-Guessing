using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class FBEventScript : MonoBehaviour {

	public string url;
	private string[] lines;
	public string[] parts;
	public static  int ServerEventVersion;
	public static int ClientEventVersion;
	public static  string ServerPlayerIDCheck;
	public GameObject NotificationBar;
	public Text NotificationBarText;
	public int RandomNum;
	public static bool AllowEvent;
	public int CurrentKeys;
	public GameObject FBEventBoxOBJ;
	public int FBEventGiven;
	public string ClientPlayerIDCheck;
	public MainMenuScript MainMenu;
	public Text EventID;

	// Use this for initialization
	void Start () {
		MainMenu = GetComponent<MainMenuScript>();
		ClientEventVersion = 1;
		FBEventGiven = PlayerPrefs.GetInt("FBEventGivenSTR");
		ClientPlayerIDCheck = PlayerPrefs.GetString("CurrentPlayerID");
		StartCoroutine(StartCR());
		EventID.text = "Your Event ID is: "+ClientPlayerIDCheck;
	}
	
	IEnumerator StartCR() 
	{
		WWW www = new WWW(url);
		yield return www;
		
		if (!string.IsNullOrEmpty(www.error)) {
			Debug.Log(www.error);
		} else {
			lines = www.text.Split("\n"[0]);
			for(var d=0 ; d < lines.Length; d++  ){
				parts = (lines[d].Trim()).Split(","[0]);
				ServerPlayerIDCheck = parts[4];
				ServerEventVersion = System.Int32.Parse(parts[5]);
				if(ClientEventVersion != ServerEventVersion){
					FBEventBoxOBJ.SetActive(false);
					PlayerPrefs.SetInt("FBEventGivenSTR",0);
				}
				if(ClientEventVersion == ServerEventVersion && FBEventGiven == 0 && ServerPlayerIDCheck == ClientPlayerIDCheck){
					FBEventBoxOBJ.SetActive(true);
				}
			}
		}
	}

	public void CollectEventBtn(){
		RandomNum = Random.Range(2,5);
		PlayerPrefs.SetInt("FBEventGivenSTR",1);
		NotificationBar.SetActive(true);
		FBEventBoxOBJ.SetActive(true);
		NotificationBarText.text = "You have recieved " + RandomNum + " Keys, Have fun.";
		CurrentKeys = PlayerPrefs.GetInt ("CurrentKeys");
		CurrentKeys += RandomNum;
		PlayerPrefs.SetInt ("CurrentKeys",CurrentKeys);
		MainMenu.RefreshPoints();
		FBEventBoxOBJ.SetActive(false);
		//FBEventBoxOBJ.GetComponent<Button>().interactable = false;
		StartCoroutine(TurnOfNotification(0.5f));
	}
	
	IEnumerator TurnOfNotification(float waitTime) {
		yield return new WaitForSeconds(5.0f);
		NotificationBar.SetActive(false);
	}
}
