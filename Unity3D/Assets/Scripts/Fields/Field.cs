/* Field.
 * 2015-09-19. Leonardo Molina.
 * 2016-03-09. Last modification.
 */
 
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class Field {
	GameObject holder;
	RectTransform rect;
	Text label;
	
	Action<Field> callback;
	string name;
	
	Dictionary<string, string> dictionary = new Dictionary<string, string>();
	string prefix = "";
	string current = "";
	string last = "";
	
	object properties;
	
	Color background = new Color(1f, 1f, 1f, 1f);
	bool enabled = true;
	
	public Field(Action<Field> callback, string name, string value) {
		Callback = callback;
		Name = name;
		String = value;
	}
	
	public GameObject Holder {
		set {
			holder = value;
			holder.name = name;
			rect = holder.GetComponent<RectTransform>();
		}
		get {
			return holder;
		}
	}
	
	public Action<Field> Callback {
		set {
			callback = value;
		}
		get {
			return callback;
		}
	}
	
	public string Name {
		set {
			name = value;
		}
		get {
			return name;
		}
	}
	
	public string Prefix {
		set {
			prefix = value;
			UpdateLabel();
		}
		get {
			return prefix;
		}
	}
	
	public string String {
		set {
			Current = value;
			last = value;
			OnString();
		}
		get {
			return current;
		}
	}
	
	protected string Current {
		set {
			current = value;
			UpdateLabel();
		}
		get {
			return current;
		}
	}
	
	void UpdateLabel() {
		if (label != null)
			label.text = prefix + (dictionary.ContainsKey(current) ? dictionary[current] : current);
	}
	
	protected virtual void OnString() {
	}
	
	float lastNumber = 0f;
	string lastString = "";
	public float Number {
		set {
			String = value.ToString("#0.0000");
		}
		get {
			float number = 0f;
			if (lastString.Equals(current)) {
				number = lastNumber;
			} else {
				float.TryParse(current, out number);
				lastString = current;
				lastNumber = number;
			}
			return number;
		}
	}
	
	public bool Changed {
		get {
			bool changed = !last.Equals(String);
			last = String;
			return changed;
		}
	}
	
	public object Properties {
		set {
			properties = value;
		}
		get {
			return properties;
		}
	}
	
	public virtual bool Enabled {
		set {
			enabled = value;
		}
		get {
			return enabled;
		}
	}
	
	public virtual Color Background {
		set {
			background = value;
		}
		get {
			return background;
		}
	}
	
	public void Rewrite(string from, string to) {
		dictionary[from] = to;
		UpdateLabel();
	}
	
	public bool Equals(string value) {
		return String.Equals(value);
	}
	
	public void Position(Transform parent, float x, float y, float width, float height) {
		holder.transform.SetParent(parent, true);
		rect.localScale = new Vector3(1f, 1f, 1f);
		rect.anchoredPosition = new Vector2(x, y);
		rect.sizeDelta = new Vector2(width, height);
	}
	
	public void ScaleXSetY(Transform parent, float left, float right, float y, float height) {
		float na = 0f; // Not apply.
		holder.transform.SetParent(parent, true);
		rect.localScale = new Vector3(1f, 1f, 1f);
		// ymin=1 ymax=1 --> relative to top
		// ymin=0 ymax=0 --> relative to bottom
		rect.anchorMin = new Vector2(left, 1f);
		rect.anchorMax = new Vector2(right, 1f);
		rect.offsetMin = new Vector2(0f, na);
		rect.offsetMax = new Vector2(0f, na);
		rect.anchoredPosition = new Vector2(na, y);
		rect.sizeDelta = new Vector2(na, height);
	}
	
	public Text Label {
		set {
			label = value;
		}
		get {
			return label;
		}
	}
}