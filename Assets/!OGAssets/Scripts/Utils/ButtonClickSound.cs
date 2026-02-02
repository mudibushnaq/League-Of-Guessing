using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent (typeof (Button))]
public class ButtonClickSound : MonoBehaviour {

	public AudioClip sound;
	// Use this for initialization
	void Start () {
		gameObject.GetComponent<Button>().onClick.AddListener(new UnityEngine.Events.UnityAction(() =>playSfx()));
	}

	void playSfx(){
		if(sound!=null){
			//AudioManager.Instance.PlayEffect(sound);
		}
	}


}
