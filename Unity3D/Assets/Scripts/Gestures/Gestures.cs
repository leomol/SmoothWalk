/* 
 * 2015-09-19. Leonardo Molina.
 * 2019-08-05. Last modification.
 */

/*
	Coordinate system is left-handed, like in Unity:
		Clockwise rotations around 3D.y are positive.
		3D.y is a normal vector from the tablet towards the user.
	Interpretation of 2D vectors (e.g. position, linear speed):
		2D.x and 2D.y are aligned with width and height.
		2D.x corresponds to 3D.x whereas 2D.y corresponds to 3D.z.
	Computations assume gesture is relative to center of the screen. Coordinate system is like unity: unity-y-plane comes off the screen, towards the user, +unity-z is tablet-up, +unity-z is tablet-right. Rotations around unity-y: left hand where axis of rotation is thumb.
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pointers;

public class Gestures : MonoBehaviour {
	// Speed computed by averaging traces (delta positions) from individual pointers, over time.
	PointerDebounce pointerDebounce;
	PointerGetter pointerGetter;
	Vector2 dragLinearSpeed;
	Vector2 hoverLinearSpeed;
	
	// Linear speed from dragging (mouse click and touch combined).
	public Vector2 DragLinearSpeed {
		get {
			return dragLinearSpeed;
		}
	}
	
	// Linear speed from hovering (one or more computer mice combined, when supported).
	public Vector2 HoverLinearSpeed {
		get {
			return hoverLinearSpeed;
		}
	}
	
	void Awake() {
		pointerDebounce = Components.Get("PointerDebounce") as PointerDebounce;
		//pointerDebounce = Components.gObject.GetComponent<PointerDebounce>();
		pointerGetter = Components.Get("PointerGetter") as PointerGetter;
	}
	
	void Update() {
		List<Pointer> pointers = pointerDebounce.Pointers;
		
		dragLinearSpeed = Vector2.zero;
		foreach (Pointer pointer in pointers) {
			dragLinearSpeed += pointer.deltaPosition;
		}
		if (pointers.Count > 0) {
			dragLinearSpeed /= pointers.Count;
		}
		dragLinearSpeed /= Time.deltaTime;
		
		pointers = pointerGetter.HoverPointers;
		hoverLinearSpeed = Vector2.zero;
		foreach (Pointer pointer in pointerGetter.HoverPointers)
			hoverLinearSpeed += pointer.deltaPosition;
		if (pointers.Count > 0)
			hoverLinearSpeed /= pointers.Count;
		hoverLinearSpeed /= Time.deltaTime;
	}
}