/*
 * 2015-09-19. Leonardo Molina.
 * 2017-09-25. Last modification.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class MainThread : MonoBehaviour {
	static readonly object accessLock = new object();
	static Queue<Action<object>> callbacks = new Queue<Action<object>>();
	static Queue<object> objects = new Queue<object>();
	
	// Most be called from the main thread.
	public static void Wake() {
		Components.Get("MainThread");
	}
	
	public static void Call(Action<object> fcn, object obj) {
		lock (accessLock) {
			callbacks.Enqueue(fcn);
			objects.Enqueue(obj);
		}
	}
	
	public static void Call(Action fcn) {
		lock (accessLock) {
			callbacks.Enqueue((x) => fcn());
			objects.Enqueue(null);
		}
	}
	
	void Update() {
		lock (accessLock) {
			while (callbacks.Count > 0)
				callbacks.Dequeue()(objects.Dequeue());
		}
	}
}