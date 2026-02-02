using UnityEngine;
using System.Collections;
using UnityEngine.UI;

//[ExecuteInEditMode]
public class FollowTextSize : MonoBehaviour {

	public Text followed;

	float scaleFactor = 1;
	int followedSize=0;
	void Start(){
		scaleFactor =GetComponentInParent<Canvas>().scaleFactor;

	}

	// Update is called once per frame
	void Update () {
		//scaleFactor =GetComponentInParent<Canvas>().scaleFactor;
		followedSize = (int)(((float)followed.cachedTextGenerator.fontSizeUsedForBestFit)/scaleFactor);
		if(followed!=null && followedSize>0 && followedSize != GetComponent<Text>().fontSize){
			//Debug.Log( gameObject.name+" : "  + followedSize +"  "+ GetComponent<Text>().fontSize);

			GetComponent<Text>().fontSize = followedSize;
		}
	}
}
