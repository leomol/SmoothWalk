/* 
 * 2015-09-19. Leonardo Molina.
 * 2019-08-05. Last modification.
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

interface IField {
	GameObject Holder {
		set;
		get;
	}
	
	Action<Field> Callback {
		set;
		get;
	}
	
	string Name {
		set;
		get;
	}
	
	string Prefix {
		set;
		get;
	}
	
	string String {
		set;
		get;
	}
	
	float Number {
		set;
		get;
	}
	
	void Rewrite(string from, string to);
	
	bool Equals(string value);
	
	void Position(Transform parent, float x, float y, float width, float height);
	
	Text Label {
		set;
		get;
	}
}