/* InputField.
 * 2015-09-19. Leonardo Molina.
 * 2016-10-04. Last modification.
 */

using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class TextField : Field {
	InputField input;
	bool idle = true;
	
	public TextField(Action<Field> callback, string name, string current) : base(callback, name, current) {
		Holder = (GameObject) GameObject.Instantiate(Resources.Load("Fields/TextField"));
		input = Holder.GetComponent<InputField>();
		
		UnityAction<string> action = (string value) => {OnChange();};
		input.onEndEdit.AddListener(action);
	}
	
	void OnChange() {
		if (idle) {
			Current = input.text;
			Callback(this);
		}
	}
	
	protected override void OnString() {
		if (input != null) {
			idle = false;
			input.text = String;
			idle = true;
		}
	}
	
	public string Hint {
		set {
			input.placeholder.GetComponent<Text>().text = value;
		}
		get {
			return input.placeholder.GetComponent<Text>().text;
		}
	}
	
	public bool MultiLine {
		set {
			input.lineType = value ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
		}
		get {
			return input.lineType == InputField.LineType.MultiLineNewline;
		}
	}
	
	public InputField.ContentType ContentType {
		set {
			input.contentType = value;
		}
		get {
			return input.contentType;
		}
	}
	
	public int Limit {
		set {
			input.characterLimit = value;
		}
		get {
			return input.characterLimit;
		}
	}
	
	public override bool Enabled {
		get {
			return base.Enabled;
		}
		set {
			base.Enabled = value;
			input.interactable = value;
		}
	}
	
	public override Color Background {
		get {
			return base.Background;
		}
		set {
			base.Background = value;
			input.image.color = value;
		}
	}
}