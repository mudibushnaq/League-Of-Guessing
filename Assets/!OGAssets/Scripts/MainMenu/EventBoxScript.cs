using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using System.IO;
public class EventBoxScript : MonoBehaviour {
	
	public string url;
	private string[] lines;
	public string[] parts;
	public static  int ServerEventVersion;
	public static int ClientEventVersion;
	public GameObject NotificationBar;
	public Text NotificationBarText;
	public int RandomNum;
	public static bool AllowEvent;
	public int CurrentIP;
	public GameObject EventBoxOBJ;
	public int BoxUsed;
	// Use this for initialization
	void Start () {
		ClientEventVersion = 1;
		BoxUsed = PlayerPrefs.GetInt("BoxUsedSTR");
		StartCoroutine(StartCR());
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
					ServerEventVersion = System.Int32.Parse(parts[3]);
					if(ClientEventVersion != ServerEventVersion){
						EventBoxOBJ.SetActive(false);
						PlayerPrefs.SetInt("BoxUsedSTR",0);
					}
					if(ClientEventVersion == ServerEventVersion && BoxUsed == 0){
						EventBoxOBJ.SetActive(true);
					}
				}
			}
		}
	
	public void CollectEventBtn(){
		RandomNum = Random.Range(1,10);
		PlayerPrefs.SetInt("BoxUsedSTR",1);
		NotificationBar.SetActive(true);
		EventBoxOBJ.SetActive(true);
		NotificationBarText.text = "You have recieved " + RandomNum + " IP, Have fun.";
		CurrentIP = PlayerPrefs.GetInt ("CurrentIP");
		CurrentIP += RandomNum;
		PlayerPrefs.SetInt ("CurrentIP",CurrentIP);
		EventBoxOBJ.GetComponent<Button>().interactable = false;
		StartCoroutine(TurnOfNotification(5.0f));
	}

	IEnumerator TurnOfNotification(float waitTime) {
		yield return new WaitForSeconds(5.0f);
		NotificationBar.SetActive(false);
		EventBoxOBJ.SetActive(false);
	}
}
