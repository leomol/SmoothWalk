/* 
 * 2015-09-19. Leonardo Molina.
 * 2019-08-05. Last modification.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Components {
	public static GameObject gObject;
	public static Component Get(string name) {
			if (gObject == null) {
				gObject = new GameObject("Components");
				GameObject.DontDestroyOnLoad(gObject);
			}
			Component component = gObject.GetComponent(name);
			if (component == null)
				component = gObject.PushComponent(name);
			return component;
	}
}

static class ComponentHelper {
	static Dictionary<string, System.Type> knownComponents;
	static System.Type GetComponentTypeByName(string name) {
		if (string.IsNullOrEmpty(name))
			return null;
 
		if (knownComponents == null) {
			knownComponents = new Dictionary<string, System.Type>();
			var ctp = typeof(UnityEngine.Component);
			foreach(var assemb in System.AppDomain.CurrentDomain.GetAssemblies()) {
				foreach(var tp in assemb.GetTypes()) {
					if (ctp.IsAssignableFrom(tp))
						knownComponents.Add(tp.FullName, tp);
				}
			}
		}
		return (knownComponents.ContainsKey(name)) ? knownComponents[name] : null;
	}
 
	public static Component PushComponent(this UnityEngine.GameObject go, string name) {
		if (go == null)
			throw new System.ArgumentNullException("go");
		var tp = GetComponentTypeByName(name);
		return (tp != null) ? go.AddComponent(tp) : null;
	}
}