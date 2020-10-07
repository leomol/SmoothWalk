/*
	2016-04-25. Leonardo Molina.
	2016-04-25. Last modification.
*/

using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class Player : MonoBehaviour {
	CharacterController controller;
	
	bool towardsPosition = false;
	Vector3 targetPosition = Vector3.zero;
	float lSpeedTarget = 0f;
	Vector3 lSpeed = Vector3.zero;
	
	bool towardsRotation = false;
	Quaternion targetQuaternion = new Quaternion(0f,0f,0f,0f);
	float rSpeedTarget = 0f;
	Vector3 rSpeed = Vector3.zero;
	
	public void Start() {
		targetPosition = transform.position;
		targetQuaternion = Quaternion.Euler(Euler);
		controller = GetComponent<CharacterController>();
	}
	
	public void Update() {
		if (towardsRotation)
			transform.rotation = Quaternion.RotateTowards(transform.rotation, targetQuaternion, rSpeedTarget*Time.deltaTime);
		else
			transform.Rotate(rSpeed.x*Time.deltaTime, rSpeed.y*Time.deltaTime, rSpeed.z*Time.deltaTime);
		
		if (towardsPosition)
			transform.position = Vector3.MoveTowards(transform.position, targetPosition, lSpeedTarget*Time.deltaTime);
		else
			controller.SimpleMove(lSpeed);
		
		// Drop the character until hit.
		if (ForceGround)
			controller.Move(-4.9f*Vector3.up*Time.deltaTime);
	}
	
	float nextGroundCheck = 0f;
	public bool ForceGround {
		get {
			if (!controller.isGrounded || Control.RunTime > nextGroundCheck) {
				nextGroundCheck = Control.RunTime + 1f;
				return true;
			} else {
				return false;
			}
		}
	}
	
	public void MoveTowards(Vector3 position, float speed) {
		towardsPosition = true;
		lSpeedTarget = speed;
		targetPosition = position;
	}
	
	public void RotateTowards(Vector3 rotation, float speed) {
		towardsRotation = true;
		rSpeedTarget = speed;
		targetQuaternion = Quaternion.Euler(rotation);
	}
	
	public Vector3 LinearSpeed {
		get {
			return lSpeed;
		}
		set {
			if (value != lSpeed)
				towardsPosition = false;
			lSpeed = value;
		}
	}
	
	public Vector3 AngularSpeed {
		get {
			return rSpeed;
		}
		set {
			if (value != rSpeed)
				towardsRotation = false;
			rSpeed = value;
		}
	}
	
	public Vector3 Position {
		get {
			return transform.position;
		}
		set {
			targetPosition = value;
			transform.position = value;
		}
	}
	
	public Quaternion Rotation {
		get {
			return transform.rotation;
		}
		set {
			transform.rotation = value;
		}
	}
	
	public Vector3 Euler {
		get {
			return transform.rotation.eulerAngles;
		}
		set {
			transform.rotation = Quaternion.Euler(value);
		}
	}
}