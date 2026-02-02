#if UNITY_EDITOR
using UnityEngine;
using System.Collections;
using UnityEditor;

[CustomEditor(typeof(EventTriggerToEvent))]
public class EventTriggerToEventEditor : Editor {

	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();
		
		EventTriggerToEvent myScript = (EventTriggerToEvent)target;
		if(GUILayout.Button("Run"))
		{
			Debug.Log("dddd");
			myScript.run();


		}
	}
}

#endif
