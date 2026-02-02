

using UnityEngine;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Events;
#endif
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class EventTriggerToEvent : MonoBehaviour {

	public GameObject main;
	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
	
	}

	public void run(){
		#if UNITY_EDITOR
		Debug.Log("Here we go");
		IconsScriptSHOP script = main.GetComponent<IconsScriptSHOP>();

		Debug.Log("Script "+script);
	//	for (int i=0; i<transform.childCount; i++){
			Transform c = transform.GetChild(0);
		Debug.Log("Working on "+c.name);
			Button btn= c.GetComponent<Button>();
			EventTrigger et = c.GetComponent<EventTrigger>();
			if(btn!=null && et!=null){
//			btn.onClick.AddPersistentListener(delegate { script.OnClicked(btn);});
			//UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, new UnityAction(() =>script.OnClicked(btn)));
//			UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, new UnityAction(script.OnClicked));
			System.Type[] t = new System.Type[1];
			t[0]=typeof(Button);

			System.Reflection.MethodInfo targetInfo = UnityEvent.GetValidMethodInfo(script,"OnClicked",t);
//			dd targetInfo = UnityEvent.GetValidMethodInfo(yourComponentInstance ,MethodName , MethodParam);
			UnityAction methodDelegate = System.Delegate.CreateDelegate(typeof(UnityAction), script, targetInfo) as UnityAction;
			UnityEventTools.AddPersistentListener(btn.onClick, methodDelegate);


			Debug.Log("Inside on "+c.name);
//				Destroy(et);
			}
		//}
		#endif
	}


}

