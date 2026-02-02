using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HideNotification : MonoBehaviour {

	bool ifActive;

	public void LateUpdate(){
		if (this.gameObject.activeInHierarchy && ifActive == false) {
			ifActive = true;
		}
		if (ifActive == true) {
			CheckifON ();
			ifActive = false;
		}
	}

	public void CheckifON() {
		if(this.gameObject.activeInHierarchy){
			StartCoroutine (HideThis (3.0F));
			//Debug.Log ("Notification is ON");
		}
	}
	IEnumerator HideThis(float waitTime) {
			yield return new WaitForSeconds (3.0f);
				if (this.gameObject.activeInHierarchy) {
				this.gameObject.SetActive (false);
				//Debug.Log ("Notification is OFF");
			}
	}
}
