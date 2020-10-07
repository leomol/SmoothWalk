/* 
 * 2015-09-19. Leonardo Molina.
 * 2019-03-21. Last modification.
 */
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pointers;

public class PointerGetter : MonoBehaviour {
	// Generic holder for touch or mouse events.
	int dragCount = 0;
	List<Pointer> dragPointers = new List<Pointer>(20);
	Pointer mouseDrag = new Pointer();
	
	int hoverCount = 0;
	List<Pointer> hoverPointers = new List<Pointer>(2);
	Pointer mouseHover = new Pointer();
	
	void Update() {
		float tic = Time.time;
		dragPointers = new List<Pointer>(20);
		bool useTouch = Input.touchCount != 0;
		if (useTouch) {
			foreach (Touch touch in Input.touches)
				dragPointers.Add(new Pointer(tic, touch));
		}
		// Always update mouse.
		Vector2 mousePosition = (Vector2) Input.mousePosition;
		
		bool mouseRise = Input.GetMouseButtonUp(0);
		bool mouseFall = Input.GetMouseButtonDown(0);
		bool mouseDown = Input.GetMouseButton(0);
		bool mouseUp = !mouseDown;
		if (mouseFall) {
			// Began.
			mouseDrag.deltaPosition = Vector2.zero;
			mouseDrag.position = mousePosition;
			mouseDrag.deltaTime = 0f;
			mouseDrag.time = tic;
			mouseDrag.phase = TouchPhase.Began;
			if (!useTouch)
				dragPointers.Add(mouseDrag);
		} else if (mouseDown || mouseRise) {
			// Moving / Stationary || End.
			mouseDrag.deltaPosition = mousePosition - mouseDrag.position;
			mouseDrag.position = mousePosition;
			mouseDrag.deltaTime = tic - mouseDrag.time;
			mouseDrag.time = tic;
			mouseDrag.phase = mouseRise ? TouchPhase.Ended : (mouseDrag.deltaPosition.sqrMagnitude > 0f ? TouchPhase.Moved : TouchPhase.Stationary);
			
			// Use mouse unless using touch or mouse is hovering.
			if (!useTouch)
				dragPointers.Add(mouseDrag);
		}
		dragCount = dragPointers.Count;
		//Debug.Log("PointerGetter->DragCount:" + dragCount); // !!
		
		hoverPointers = new List<Pointer>(2);
		if (mouseRise) {
			// Began.
			mouseHover.deltaPosition = Vector2.zero;
			mouseHover.position = mousePosition;
			mouseHover.deltaTime = 0f;
			mouseHover.time = tic;
			mouseHover.phase = TouchPhase.Began;
			mouseHover.fingerId = 0;
			hoverPointers.Add(mouseHover);
		} else if (mouseUp || mouseFall) {
			// Moving / Stationary || End.
			mouseHover.deltaPosition = mousePosition - mouseHover.position;
			mouseHover.position = mousePosition;
			mouseHover.deltaTime = tic - mouseHover.time;
			mouseHover.time = tic;
			mouseHover.phase = mouseFall ? TouchPhase.Ended : (mouseHover.deltaPosition.sqrMagnitude > 0f ? TouchPhase.Moved : TouchPhase.Stationary);
			mouseHover.fingerId = 0;
			hoverPointers.Add(mouseHover);
		}
		hoverCount = hoverPointers.Count;
	}
	
	public int DragCount {
		get {
			return dragCount;
		}
	}
	
	public int HoverCount {
		get {
			return hoverCount;
		}
	}
	
	public List<Pointer> DragPointers {
		get {
			List<Pointer> pointers = new List<Pointer>(dragPointers.Count);
			foreach (Pointer pointer in dragPointers)
				pointers.Add(pointer.ShallowCopy());
			return pointers;
		}
	}
	
	public List<Pointer> HoverPointers {
		get {
			List<Pointer> pointers = new List<Pointer>(hoverPointers.Count);
			foreach (Pointer pointer in dragPointers)
				pointers.Add(pointer.ShallowCopy());
			return pointers;
		}
	}
}