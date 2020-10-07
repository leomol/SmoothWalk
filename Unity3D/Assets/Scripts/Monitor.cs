/* 
 * 2015-09-19. Leonardo Molina.
 * 2019-08-05. Last modification.
 */

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class Monitor : MonoBehaviour {
	public Toggle forwardInputsUI;
	public Dropdown sourceUI;
	public Text messageUI;
	string choice = "";
	string source = "";
	bool error = false;
	bool forwardInputs = false;
	
	public void OnEnable() {
		// Load preferences.
		choice = PlayerPrefs.GetString("playerChoice", "<any>");
		source = PlayerPrefs.GetString("playerSource", "");
		ForwardInputs = PlayerPrefs.GetString("forwardInputs", "0").Equals("1");
		
		// Update network manager.
		StopCoroutine(Ticker());
		StartCoroutine(Ticker());
	}
	
	public void SourceChanged() {
		choice = sourceUI.options[sourceUI.value].text.Replace("*", "");
		Ticker();
	}
	
	bool RefreshSources(string choice, List<string> options) {
		// Use separate copy.
		List<string> sources = new List<string>(options);
		
		// Append none and any to sources.
		sources.Insert(0, "<none>");
		sources.Insert(1, "<any>");
		
		// Append special option: Use when available.
		int choiceIndex = sources.IndexOf(choice);
		bool listed = choiceIndex != -1;
		if (listed) {
			choiceIndex = sources.IndexOf(choice);
		} else {
			choiceIndex = 2;
			sources.Insert(choiceIndex, choice + "*");
		}
		
		// Turn string list into a OptionData list.
		List<Dropdown.OptionData> optionData = new List<Dropdown.OptionData>();
		for (int i = 0; i < sources.Count; i++) {
			string current = sources.ElementAt(i);
			optionData.Add(new Dropdown.OptionData(current));
		}
		
		// Update GUI.
		sourceUI.ClearOptions();
		sourceUI.options = optionData;
		sourceUI.value = choiceIndex;
		
		return listed;
	}
	
	IEnumerator Ticker() {
		while (true) {
			List<string> options = Global.Network.Players;
			if (RefreshSources(choice, options)) {
				switch (choice) {
					case "<any>":
						// When any is allowed and at least one available.
						if (options.Count > 0)
							source = options.ElementAt(0);
						break;
					case "<none>":
						source = "";
						break;
					default:
						source = choice;
						break;
				}
			}
			Global.Network.Player = source;
			yield return new WaitForSeconds(1f);
		}
	}
	
	public void Apply() {
		SetMessage();
		if (!error) {
			messageUI.text = "";
			PlayerPrefs.SetString("playerChoice", choice);
			PlayerPrefs.SetString("playerSource", source);
			PlayerPrefs.SetString("forwardInputs", ForwardInputs ? "1" : "0");
			PlayerPrefs.Save();
			Visible(false);
		}
	}
	
	public void Reset() {
		sourceUI.value = 1;
		Ticker();
		
		SetMessage();
	}
	
	void SetMessage() {
		if (error)
			messageUI.text = "Invalid player ID.";
		else
			messageUI.text = "";
	}
	
	public void Visible(bool visibility) {
		Global.Menu.Hide(gameObject);
	}
	
	public string Choice {
		get {
			return choice;
		}
	}
	
	public void ForwardInputsChanged() {
		ForwardInputs = forwardInputsUI.isOn;
	}
	
	public bool ForwardInputs {
		get {
			return forwardInputs;
		}
		set {
			forwardInputs = value;
			if (forwardInputsUI.isOn != value)
				forwardInputsUI.isOn = value;
		}
	}
}