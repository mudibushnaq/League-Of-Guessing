using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using I2.Loc;

public class ProfileController : MonoBehaviour
{
	// ChampionsMode // SkinsMode Score // ItemsMode Score
	// How many tried he tried (Done Button Clicks) // 
	// How Many Levels He Finished // 
	// How many keys he used //
	// how much ip he spent //

	// how many wrong answers he made //
	// how many right answers he made //
	// how many total hearts he got and the ability to add more hearts //

	// how many keys he used in items mode //
	// how many right and wrong answers he made //

	// what is his current rank / how many points he want for the next rank //


	public GameObject PlayerRank;
	public Text PlayerName;
	public Text WrongAnswersText, RightAnswersText, CurrentLevel, KeysUsedText, IPSpentText;

	void OnEnable ()
	{
		PlayerName.text = PlayerPrefs.GetString (PPrefsConstants.PlayerName);
		LoadInfo ();
	}

	void LoadInfo(){
		//PlayerRank.GetComponent<Image> ().sprite = CurrentRankImage;
	}

	public void ChampModeLoad(){
		Debug.Log ("Load Champion Mode Data");
	}

	public void SkinsModeLoad(){
		Debug.Log ("Load Skins Mode Data");
	}

	public void ItemsModeLoad(){
		Debug.Log ("Load Items Mode Data");
	}

}
