using UnityEngine;
using System.Collections;
using System.Runtime.InteropServices;
using System.Collections.Generic;

/**
 * This version of Presage's Wrapper was tested with 1.6.0 Presage lib
 */
public class WPresage : MonoBehaviour {
#if UNITY_ANDROID
	private const string WPRESAGE_ID = "io.presage.Presage";
	private const string WHANDLER_ID = "io.presage.utils.IADHandler";

	// Method used to Initialize Presage SDK
	public static void Initialize() {
		AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
		AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
		
		activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
       	{
			AndroidJavaClass presageClass = new AndroidJavaClass(WPRESAGE_ID);
			AndroidJavaObject presage = presageClass.CallStatic<AndroidJavaObject> ("getInstance");
			presage.Call("setContext", activity);
			presage.Call("start");
		}));
	}
	
	/**
	 * Method used to Call AdToServ to show interstitial
	 */
	public static void AdToServe(string name, IADHandler handler) {
		AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
		AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
		
		activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
       	{
			AndroidJavaClass presageClass = new AndroidJavaClass(WPRESAGE_ID);
			AndroidJavaObject presage = presageClass.CallStatic<AndroidJavaObject> ("getInstance");
			IADHandlerProxy proxy = new IADHandlerProxy (handler);
			presage.Call("adToServe", name, proxy);
		}));
	}
	
	/**
	 * Proxy to interface with the IADHandler of the jar
	 */
	public class IADHandlerProxy : AndroidJavaProxy {
		
		delegate void AdNotFound();
		delegate void AdFound();
		delegate void AdClosed();
		
		AdNotFound adNotFound;
		AdFound adFound;
		AdClosed adClosed;
		
		public IADHandlerProxy(IADHandler handler) : base(WHANDLER_ID) {
			adNotFound = handler.OnAdNotFound;
			adFound = handler.OnAdFound;
			adClosed = handler.OnAdClosed;
		}
		
		void onAdNotFound() {
			// Add was'nt found
			adNotFound();
		}
		
		void onAdFound() {
			// Add was found
			adFound();
		}

		void onAdClosed() {
			// Add was closed
			adClosed();
		}
	}
	
	/**
	 * Interface to be implemented by the app
	 */
	public interface IADHandler {
		void OnAdNotFound();
		void OnAdFound();
		void OnAdClosed();
	}
#endif
}
