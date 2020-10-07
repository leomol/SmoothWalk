/* 
 * 2015-09-19. Leonardo Molina.
 * 2019-03-21. Last modification.
 */

using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class GlobalInstance : MonoBehaviour {
	// Attached to another object.
	public AudioSource audioSource;
	public AutoAlign autoAlign;
	public Monitor monitor;
	public Player player;
	
	void Awake() {
		MainThread.Wake();
		
		// Attached to this object.
		Global.Control = GetComponent<Control>();
		Global.Menu = GetComponent<Menu>();
		Global.Network = GetComponent<Network>();
		Global.Instance = this;
		
		// Attached to another object.
		Global.AudioSource = audioSource;
		Global.AutoAlign = autoAlign;
		Global.Monitor = monitor;
		Global.Player = player;
		
		// Started manually. [hard-code-3]
		Global.Network.LocalPort = 24000;
		Global.Network.Port = 25000;
		Global.Network.Handshake = "#SW5#";
		
		// Variables.
		// Units per pixel (e.g. cm per pixel).
		Global.PPU = Screen.dpi/2.54f;
		Global.UPP = 1f/Global.PPU;
	}
	
	IEnumerator KillCoroutine() {
		yield return new WaitForSeconds(0.2f);
		System.Diagnostics.Process.GetCurrentProcess().Kill();
	}
	
	public void Quit() {
		ForceQuit();
	}
	
	// Workaround to make InputFields draw correctly in Windows Unity.
	public void Flicker(GameObject gObj) {
		IEnumerator pokeRoutine = FlickerRoutine(gObj);
		StartCoroutine(pokeRoutine);
	}
	
	IEnumerator FlickerRoutine(GameObject gObj) {
		gObj.SetActive(false);
		yield return new WaitForEndOfFrame();
		gObj.SetActive(true);
		yield return null;
	}
	
	void ForceQuit() {
		#if UNITY_EDITOR
		EditorApplication.isPlaying = false;
		#else 
		Application.Quit();
		#endif
		
		//# On Windows, force exit because the process won't stop gracefully on W8.
		// Wait a few milliseconds so that other scripts finish first.
		if (Application.platform == RuntimePlatform.WindowsPlayer)
			StartCoroutine(KillCoroutine());
	}
	
	void OnApplicationQuit() {
	}
}