/* 
 * 2015-09-19. Leonardo Molina.
 * 2019-08-05. Last modification.
 */

using Microsoft.Win32;
using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class Keyboard : MonoBehaviour {
	string targetPath;
	
	void Awake() {
		if (Tools.IsWindows) {
			UIEventHandler events = gameObject.AddComponent<UIEventHandler>();
			events.Callback = UIEvent;
		}
	}
	
	void UIEvent(UIEventHandler.Events eventType) {
		if (eventType == UIEventHandler.Events.Select) {
			Global.Instance.Flicker(gameObject);
			if (Global.Control.FlexKeyboard)
				Tools.ShowOnScreenKeyboard();
		}
	}
}