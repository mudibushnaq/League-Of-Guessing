using UnityEngine;
using System.Collections;
using DG.Tweening;
using UnityEngine.UI;
using System;

public class CorrectAnswerController : MonoBehaviour {

	public Image  center;
	public GameObject effect;
	public Sprite wrongAnswer;
	public Sprite corretAnswer,doubleKill,tripleKill,quadraKill,pentaKill,hexaKill;
	private Coroutine myCoroutine;
	// Use this for initialization
	void Start () {
		GetComponent<Image>().enabled = false;
		center.gameObject.SetActive(false);
		effect.gameObject.SetActive(false);
		/*effectPS = effect.GetComponent<PDUnity>();

		effectPS.AutoLoop = false;
		effectPS.Running = false;*/
//		rightWingPS.transform.localScale = Vector3.zero;

		center.color = Color.clear;
//		leftWing.color = Color.clear;
//		rightWing.color = Color.clear;
	}
	
	// Update is called once per frame
	void Update () {
	
	}

	bool showExtraEffects=false;
	public void PlayAnimation(int kills=1,Action completedAnimation=null){
		Sprite sprite=null;
		switch(kills){
		case 1:
			sprite = corretAnswer;
			showExtraEffects=false;
			break;
		case 2:
			sprite = doubleKill;
			showExtraEffects=true;
			break;
		case 3:
			sprite= tripleKill;
			showExtraEffects=true;
			break;
		case 4:
			sprite= quadraKill;
			showExtraEffects=true;
			break;
		case 5:
			sprite = pentaKill;
			showExtraEffects=true;
			break;
		case 6:
			sprite = hexaKill;
			showExtraEffects=true;
			break;
		}
		if (myCoroutine != null) {
			StopCoroutine (myCoroutine);
		}
		myCoroutine  = StartCoroutine(internalAnim(showExtraEffects,completedAnimation,sprite));
	}

	public void PlayAnimation_New(int kills = 1)
	{
		Sprite sprite = null;
		switch (kills)
		{
			case 1:
				sprite = corretAnswer;
				showExtraEffects = false;
				break;
			case 2:
				sprite = doubleKill;
				showExtraEffects = true;
				break;
			case 3:
				sprite = tripleKill;
				showExtraEffects = true;
				break;
			case 4:
				sprite = quadraKill;
				showExtraEffects = true;
				break;
			case 5:
				sprite = pentaKill;
				showExtraEffects = true;
				break;
			case 6:
				sprite = hexaKill;
				showExtraEffects = true;
				break;
		}
		if (myCoroutine != null)
		{
			StopCoroutine(myCoroutine);
		}
		myCoroutine = StartCoroutine(internalAnim_New(showExtraEffects, sprite));
	}

	IEnumerator internalAnim_New(bool showExtraEffects, Sprite sprite)
	{
		center.sprite = sprite;
		GetComponent<Image>().enabled = true;

		if (showExtraEffects)
		{
			effect.gameObject.SetActive(true);
			//effectPS.Running = true;
		}

		yield return new WaitForSeconds(0.25f);

		center.gameObject.SetActive(true);
		center.DOColor(Color.white, 0.5f);

		yield return new WaitForSeconds(2f);


		//effectPS.Running = false;


		center.DOColor(Color.clear, 0.5f);
		GetComponent<Image>().enabled = false;
		yield return new WaitForSeconds(0.5f);

		center.gameObject.SetActive(false);
		effect.gameObject.SetActive(false);
		//effectPS.AutoLoop = false;
		//effectPS.Running = false;

		center.color = Color.clear;
		GetComponent<Image>().raycastTarget = true;
		myCoroutine = null;
	}


	public void PlayWAnswerAnimation(Action onComplete=null){
		GetComponent<Image> ().raycastTarget = false;
		Sprite sprite = wrongAnswer;
		if (myCoroutine != null) {
			StopCoroutine (myCoroutine);
		}
		myCoroutine =StartCoroutine(internalAnim(false,onComplete,sprite));
	}

	public void PlayWAnswerAnimation_new()
	{
		GetComponent<Image>().raycastTarget = false;
		Sprite sprite = wrongAnswer;
		if (myCoroutine != null)
		{
			StopCoroutine(myCoroutine);
		}
		myCoroutine = StartCoroutine(internalAnim_New(false, sprite));
	}

	IEnumerator internalAnim(bool showExtraEffects,Action completedAnimation,Sprite sprite){
		center.sprite = sprite;
		GetComponent<Image>().enabled = true;

		if(showExtraEffects){
			effect.gameObject.SetActive(true);
			//effectPS.Running = true;
		}

		yield return new WaitForSeconds(0.25f);

		center.gameObject.SetActive(true);
		center.DOColor(Color.white,0.5f);

		yield return new WaitForSeconds(2f);


		//effectPS.Running = false;
		

		center.DOColor(Color.clear,0.5f);
		GetComponent<Image>().enabled = false;
		yield return new WaitForSeconds(0.5f);

		center.gameObject.SetActive(false);
		effect.gameObject.SetActive(false);
		//effectPS.AutoLoop = false;
		//effectPS.Running = false;
		
		center.color = Color.clear;

		if(completedAnimation!=null){
			completedAnimation();
		}
		GetComponent<Image> ().raycastTarget = true;
		myCoroutine = null;
	}
}
