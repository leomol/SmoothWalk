/* ListField.
 * 2015-09-19. Leonardo Molina.
 * 2016-08-30. Last modification.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;


public class ListField : Field {
	Dropdown dropdown;
	bool idle = true;
	
	public ListField(Action<Field> callback, string name) : base(callback, name, "tmp") {
		Holder = (GameObject) GameObject.Instantiate(Resources.Load("Fields/ListField"));
		dropdown = Holder.GetComponent<Dropdown>();
		
		UnityAction<int> action = (i) => {OnChange(i);};
		dropdown.onValueChanged.AddListener(action);
	}
	
	void OnChange(int pos) {
		if (idle) {
			Current = dropdown.options[pos].text;
			Callback(this);
		}
	}
	
	protected override void OnString() {
		if (dropdown != null) {
			idle = false;
			int pos = 0;
			int count = dropdown.options.Count;
			foreach (Dropdown.OptionData data in dropdown.options) {
				if (data.text.Equals(Current))
					break;
				else
					pos++;
			}
			if (pos == count)
				dropdown.AddOptions(new List<string>(){Current});
			dropdown.value = pos;
			idle = true;
		}
	}
	
	public override bool Enabled {
		get {
			return base.Enabled;
		}
		set {
			base.Enabled = value;
			dropdown.interactable = value;
		}
	}
	
	public override Color Background {
		get {
			return base.Background;
		}
		set {
			base.Background = value;
			dropdown.image.color = value;
		}
	}
}