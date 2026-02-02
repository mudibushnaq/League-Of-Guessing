using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using DG.Tweening;
using System;

public class WinAnimationController : MonoBehaviour {

	public SpriteRenderer center, leftWing, rightWing;
	public GameObject flame;
	
	void Awake(){
		GetComponent<Image>().raycastTarget = false;
		center.gameObject.SetActive(false);
		leftWing.gameObject.SetActive(false);
		rightWing.gameObject.SetActive(false);
		flame.SetActive(false);
		flame.transform.localScale = Vector3.zero;
		center.color = Color.clear;
		leftWing.color = Color.clear;
		rightWing.color = Color.clear;

	}

	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
	
	}

	public void PlayAnimation(Action completedAnimation=null){
		StartCoroutine(internalAnim(completedAnimation));
	}

	IEnumerator internalAnim(Action completedAnimation){
		GetComponent<Image>().raycastTarget = true;
		center.gameObject.SetActive(true);
		leftWing.gameObject.SetActive(true);
		rightWing.gameObject.SetActive(true);
		flame.transform.localScale=Vector3.zero;
		flame.SetActive(true);
		center.DOColor(Color.white,1.5f);
		GetComponent<Image>().DOFade(0.9f,2f);
		yield return new WaitForSeconds(1f);

		leftWing.DOColor(Color.white,1.5f);
		rightWing.DOColor(Color.white,1.5f);
		yield return new WaitForSeconds(0.25f);

		flame.transform.DOScale(0.5f,1.5f);

		yield return new WaitForSeconds(2.25f);

		center.DOColor(Color.clear,1.5f);
		GetComponent<Image>().DOFade(0f,1.5f);
		leftWing.DOColor(Color.clear,1f);
		rightWing.DOColor(Color.clear,1f);
		GetComponent<Image>().raycastTarget = false;

		if(completedAnimation!=null)
			completedAnimation();
	}
}
