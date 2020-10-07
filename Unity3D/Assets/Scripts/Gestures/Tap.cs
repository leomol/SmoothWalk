/* 
 * 2015-09-19. Leonardo Molina.
 * 2019-08-05. Last modification.
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pointers;

public class Tap : MonoBehaviour {
	public delegate void TapHandler(int nTaps, Vector2 position);
	public event TapHandler TapChanged;
	
	// State variables for tap detection.
	PointerGetter pointerGetter;
	float tapRadius;
	float tapStep;
	float tapToc = -1f;
	int lastTouchCount = 0;
	Vector2 tapCenter = Vector2.zero;
	int tapCount = 0;
	int nTaps = 0;
	
	public void Setup(float tapRadius, float tapStep) {
		this.tapRadius = tapRadius;
		this.tapStep = tapStep;
	}
	
	public Vector2 Center {
		get {
			return tapCenter;
		}
	}
	
	public int Count {
		get {
			return nTaps;
		}
	}
	
	void Awake() {
		pointerGetter = Components.Get("PointerGetter") as PointerGetter;
		Setup(Screen.dpi, 0.300f);
	}
	
	void Update() {
		if (pointerGetter.DragCount == 1) {
			Pointer pointer = pointerGetter.DragPointers[0];
			if (pointer.phase == TouchPhase.Ended) {
				if (lastTouchCount == 1) {
					nTaps = tap(Time.time, pointer.position);
					if (TapChanged != null)
						TapChanged(nTaps, pointer.position);
				} else {
					tapCount = 0;
				}
			}
		} else if (pointerGetter.DragCount > 1) {
			tapCount = 0;
		}
		lastTouchCount = pointerGetter.DragCount;
	}
	
	int tap(float time, Vector2 position) {
		if (tapCount == 0 || time > tapToc || Vector2.Distance(position, tapCenter) > tapRadius) {
			// Tap reset manually OR late untap OR tap is on time, but it's not centered.
			tapCount = 1;
			tapCenter = position;
		} else {
			// On time and centered.
			tapCount++;
		}
		tapToc = time + tapStep;
		return tapCount;
	}
}