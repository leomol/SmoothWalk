/* SliderField.
 * 2015-09-19. Leonardo Molina.
 * 2016-08-06. Last modification.
 */
 
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class SliderField : Field {
	Slider slider;
	int decimals = 0;
	bool idle = true;
	
	public SliderField(Action<Field> callback, string name, string current) : base(callback, name, current) {
		Holder = (GameObject) GameObject.Instantiate(Resources.Load("Fields/SliderField"));
		foreach (Transform child in Holder.transform) {
			switch (child.name) {
				case "Slider":
					slider = child.gameObject.GetComponent<Slider>();
					break;
				case "Text":
					Label = child.GetComponent<Text>();
					break;
			}
		}
		
		UnityAction<float> action = (float value) => {OnChange();};
		slider.onValueChanged.AddListener(action);
	}
	
	void OnChange() {
		if (idle) {
			Current = slider.value.ToString("N" + decimals);
			Callback(this);
		}
	}
	
	protected override void OnString() {
		if (slider != null) {
			idle = false;
			slider.value = Number;
			idle = true;
		}
	}
	
	public void Configure(float min, float max, int decimals) {
		idle = false;
		this.decimals = decimals;
		slider.minValue = min;
		slider.maxValue = max;
		slider.wholeNumbers = decimals == 0;
		idle = true;
	}
	
	public bool IsMax(string value) {
		float number = 0f;
		if (float.TryParse(value, out number))
			return ((int) Mathf.Round(number*decimals)) == ((int) Mathf.Round(slider.maxValue*decimals));
		else
			return false;
	}
	
	public override bool Enabled {
		get {
			return base.Enabled;
		}
		set {
			base.Enabled = value;
			slider.interactable = value;
			if (value)
				Label.color = new Color(0f, 0f, 0f, 1f);
			else
				Label.color = new Color(0.58f, 0.58f, 0.58f, 1f);
		}
	}
	
	public override Color Background {
		get {
			return base.Background;
		}
		set {
			base.Background = value;
			slider.GetComponentInChildren<Image>().color = value;
		}
	}
}