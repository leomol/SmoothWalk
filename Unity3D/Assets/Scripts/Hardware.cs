/* 
 * 2015-09-19. Leonardo Molina.
 * 2019-08-05. Last modification.
 */

using UnityEngine;
using System.Collections;

#if UNITY_ANDROID && !UNITY_EDITOR

public static class Hardware {
	public static bool IsAndroid = true;
	
	static AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
	static AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
	
	// Report changes to MediaScannerConnection (make changes available immediately through USB).
	static AndroidJavaClass intentClass = new AndroidJavaClass("android.content.Intent");
	static string action = intentClass.GetStatic<string>("ACTION_MEDIA_SCANNER_SCAN_FILE");
	static AndroidJavaClass uriClass = new AndroidJavaClass("android.net.Uri");
	static AndroidJavaObject uriObject;
	static AndroidJavaObject intentObject;
	
	static Hardware() {
		intentObject = new AndroidJavaObject("android.content.Intent", action);
	}
	
	public static void UpdateMediaScanner(string path) {
		uriObject = uriClass.CallStatic<AndroidJavaObject>("parse", "file:" + path);
		intentObject.Call<AndroidJavaObject>("setData", uriObject);
		// Method is not recommended. Search how to implement MediaScannerConnection.scanFile instead.
		activity.Call("sendBroadcast", intentObject);
	}
	
	public static void UpdateMediaScanner2(string path) {
		using (AndroidJavaObject joContext = activity.Call<AndroidJavaObject>("getApplicationContext"))
		using (AndroidJavaClass jcMediaScannerConnection = new AndroidJavaClass("android.media.MediaScannerConnection"))
		using (AndroidJavaClass jcEnvironment = new AndroidJavaClass("android.os.Environment"))
		using (AndroidJavaObject joExDir = jcEnvironment.CallStatic<AndroidJavaObject>("getExternalStorageDirectory"))
		{
			jcMediaScannerConnection.CallStatic("scanFile", joContext, new string[] {path}, null, null);
		}
	}
}

#else

public static class Hardware {
	public static bool IsAndroid = false;
	
	public static void UpdateMediaScanner(string path) {}
	public static void UpdateMediaScanner2(string path) {}
	
	public static string GetAndroidExternalFilesDir() {
		return "";
	}
}
	
#endif