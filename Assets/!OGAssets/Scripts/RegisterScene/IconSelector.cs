using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class IconSelector : MonoBehaviour
{

	public static int SelectedIcon;
	public GameObject GlowEffect;

	// Use this for initialization
	void Start ()
	{
	
	}
	
	// Update is called once per frame
	void Update ()
	{
	
	}

	public void OnClicked (Button button)
	{
		SelectedIcon = System.Int32.Parse (button.gameObject.name);
		//Debug.Log ("SelectedIcon " + SelectedIcon);
		GlowEffect.SetActive (true);
		GlowEffect.transform.SetParent (button.transform, false);
	}
}
