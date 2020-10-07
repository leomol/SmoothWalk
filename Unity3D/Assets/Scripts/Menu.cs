/*
 * Hide/Show objects.
 * If an object has a canvas, the enable property of its canvas component will change.
 * Otherwise, the active property of the gameobject will change.
 *
 * 2015-09-19. Leonardo Molina.
 * 2016-04-02. Last modification.
 */
 
using System;
using System.Linq;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Menu : MonoBehaviour {
	List<GameObject> activeList = new List<GameObject>();
	GameObject last = null;
	int visibleCount = 0;
	int added = 0;
	
	public void Replace(GameObject menu) {
		foreach (GameObject other in activeList)
			Hide(other);
		Show(menu);
	}
	
	public void Toggle() {
		// Hide active if exists; show last remembered.
		if (Active != null)
			Hide(Active);
		else if (last != null)
			Show(last);
	}
	
	public void Show(GameObject menu, int index) {
		// Increase count if not previously shown or not currently showing.
		if (!activeList.Contains(menu))
			visibleCount++;
		// Make visible.
		Canvas canvas = menu.GetComponent<Canvas>();
		if (canvas == null) {
			menu.SetActive(true);
		} else {
			// Place on requested position.
			canvas.enabled = true;
			canvas.sortingOrder = index;
		}
		// Top of focus.
		activeList.Remove(menu);
		activeList.Add(menu);
	}
	
	public void Show(params GameObject[] menus) {
		foreach (GameObject menu in menus)
			Show(menu, ++added);
	}
	
	public void Hide(params GameObject[] menus) {
		foreach (GameObject menu in menus) {
			// Make invisible.
			Canvas canvas = menu.GetComponent<Canvas>();
			if (canvas == null)
				menu.SetActive(false);
			else
				canvas.enabled = false;
			// If menu was previously showed and is currently visible.
			if (activeList.Contains(menu)) {
				// Decrease count, remove from list.
				visibleCount--;
				activeList.Remove(menu);
				// Remember last to toggle.
				if (visibleCount == 0)
					last = menu;
			}
		}
	}
	
	public GameObject Active {
		get {
			// Return element with highest focus.
			if (activeList.Count > 0)
				return activeList.ElementAt(activeList.Count - 1);
			else
				return null;
		}
	}
	
	public bool Visible {
		get {
			return visibleCount > 0;
		}
		set {
			foreach (GameObject menu in activeList) {
				Canvas canvas = menu.GetComponent<Canvas>();
				if (canvas == null)
					menu.SetActive(value);
				else
					canvas.enabled = value;
			}
		}
	}
}