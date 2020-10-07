/*
	
	2016-03-06. Leonardo Molina.
	2016-03-09. Last modification.
*/
// Do these events occur in the main thread?

using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class UIEventHandler : MonoBehaviour,
	IPointerClickHandler, IPointerDownHandler, IPointerUpHandler, 
	IPointerEnterHandler, IPointerExitHandler, ISelectHandler {
	
	public Action<Events> Callback = NullCallback;
	
	public enum Events {
		PointerEnter,
		PointerExit,
		PointerClick,
		PointerUp,
		PointerDown,
		Select,
		Deselect
	}
	
	public void OnPointerEnter(PointerEventData eventData) {
		Callback(Events.PointerEnter);
	}
	
	public void OnPointerExit(PointerEventData eventData) {
		Callback(Events.PointerExit);
	}
	
	public void OnPointerClick(PointerEventData eventData) {
		Callback(Events.PointerClick);
	}
	
	public void OnPointerUp(PointerEventData eventData) {
		Callback(Events.PointerClick);
	}
	
	public void OnPointerDown(PointerEventData eventData) {
		Callback(Events.PointerClick);
	}
	
	public void OnSelect(BaseEventData eventData) {
		Callback(Events.Select);
	}
	
	public void OnDeselect(BaseEventData eventData) {
		Callback(Events.Deselect);
	}
	
	static void NullCallback(Events etype) {}
}