using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement;
using I2.Loc;

public class SoundButton : MonoBehaviour
{

	public bool soundisON;
	public bool soundisOFF;
	public Sprite SoundON;
	public Sprite SoundOFF;

	public AudioSource BGAudioSrc;
	public AudioSource SFXAudioSrc;

	//public GameObject SoundText;
	public GameObject SoundIcon;

	void Start ()
	{
		soundisON = true;
		soundisOFF = false;
		if (PlayerPrefs.GetInt ("SoundSettings") == 1) {
			soundisOFF = true;
			soundisON = false;
			SoundIcon.GetComponent<Image> ().sprite = SoundOFF;
			BGAudioSrc.mute = true;
			SFXAudioSrc.mute = true;
		}
		if (PlayerPrefs.GetInt ("SoundSettings") == 0) {
			soundisOFF = false;
			soundisON = true;
			SoundIcon.GetComponent<Image> ().sprite = SoundON;
			BGAudioSrc.mute = false;
			SFXAudioSrc.mute = false;
		}
	}

	public void ButtonClick ()
	{
		if (soundisON == true && soundisOFF == false) {
			soundisOFF = true;
			soundisON = false;
			MuteAll ();
			SoundIcon.GetComponent<Image> ().sprite = SoundOFF;
            //SoundText.GetComponent<Localize> ().SetTerm ("SOUNDOFF_TEXT", null);
            //AnalyticsManager.Instance.logSoundSettingsEvent(1);
        } else if (soundisOFF == true && soundisON == false) {
			soundisOFF = false;
			soundisON = true;
			PlayAll ();
			SoundIcon.GetComponent<Image> ().sprite = SoundON;
            //AnalyticsManager.Instance.logSoundSettingsEvent(0);
            //SoundText.GetComponent<Localize> ().SetTerm ("SOUNDON_TEXT", null);
        }
	}

	public void MuteAll()
	{
		BGAudioSrc.mute = true;
		SFXAudioSrc.mute = true;
		PlayerPrefs.SetInt("SoundSettings", 1);
	}

	public void PlayAll()
	{
		BGAudioSrc.mute = false;
		SFXAudioSrc.mute = false;
		PlayerPrefs.SetInt("SoundSettings", 0);
	}

}
