/* 
 * 2015-09-19. Leonardo Molina.
 * 2019-08-05. Last modification.
 */

using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

public class Client : MonoBehaviour {
	public InputField monitorUI;
	public Text messageUI;
	bool error = false;
	
	public void Start() {
		// Load preferences.
		monitorUI.text = PlayerPrefs.GetString("clientMonitor", "");
		
		// Update network manager.
		SetMonitor();
	}
	
	public void SetMonitor() {
		string raw = monitorUI.text;
		if (Global.Network.Validate(raw)) {
			if (raw.Length > 0)
				Global.Network.Monitors = new List<string>(Regex.Split(raw, @"[\s,;]+"));
			else
				Global.Network.Monitors = new List<string>();
			monitorUI.text = string.Join("\n", Global.Network.Monitors.ToArray());
			error = false;
		} else {
			error = true;
		}
		SetMessage();
	}
	
	public void Apply() {
		SetMonitor();
		SetMessage();
		if (!error) {
			messageUI.text = "";
			PlayerPrefs.SetString("clientMonitor", monitorUI.text);
			PlayerPrefs.Save();
			Visible(false);
		}
	}
	
	public void Reset() {
		monitorUI.text = "";
		SetMonitor();
		
		SetMessage();
	}
	
	void SetMessage() {
		if (error)
			messageUI.text = "One or more invalid monitor IDs.";
		else
			messageUI.text = "";
	}
	
	public void Visible(bool visibility) {
		Global.Menu.Hide(gameObject);
	}
}