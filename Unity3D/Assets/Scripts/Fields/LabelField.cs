/* LabelField.
 * 2015-09-19. Leonardo Molina.
 * 2016-01-27. Last modification.
 */

using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class LabelField : Field {
	Button button;
	
	public LabelField(Action<Field> callback, string name, string current) : base(callback, name, current) {
		Holder = (GameObject) GameObject.Instantiate(Resources.Load("Fields/LabelField"));
		button = Holder.GetComponent<Button>();
		Label = Holder.GetComponent<Text>();
		
		UnityAction action = () => {OnChange();};
		button.onClick.AddListener(action);
	}
	
	void OnChange() {
		Callback(this);
	}
	
	public override bool Enabled {
		get {
			return base.Enabled;
		}
		set {
			base.Enabled = value;
			button.interactable = value;
		}
	}
	
	public override Color Background {
		get {
			return base.Background;
		}
		set {
			base.Background = value;
			button.image.color = value;
		}
	}
}