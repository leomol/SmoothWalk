/* ButtonField.
 * 2015-09-19. Leonardo Molina.
 * 2016-01-27. Last modification.
 */

using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class ButtonField : Field {
	Button button;
	
	public ButtonField(Action<Field> callback, string name, string current) : base(callback, name, current) {
		Holder = (GameObject) GameObject.Instantiate(Resources.Load("Fields/ButtonField"));
		button = Holder.GetComponent<Button>();
		Label = Holder.GetComponentInChildren<Text>();
		
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