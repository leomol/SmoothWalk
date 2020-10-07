/* Auto-align avatar when walls are hit.
 * 2016-03-06. Leonardo Molina.
 * 2016-10-13. Last modification.
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AutoAlign : MonoBehaviour {
	bool active = false;
	float speed = 0f;
	float alignSpeed = 90f;
	List<float> normals = new List<float>();
	List<float> times = new List<float>();
	float lastTurning = 1f;
	float lastNormal = 0f;
	float lastTarget = 0f;
	
	void Start() {
	}
	
	public void Cancel() {
		active = false;
		normals.Clear();
		speed = 0f;
	}
	
	void Update() {
		// Alignment enforced by collisions with objects.
		if (active) {
			// Target reached within degree resolution.
			float degreeResolution = alignSpeed*Time.deltaTime;
			if (Delta(lastTarget) < 2*degreeResolution) {
				Cancel();
			} else {
				speed = lastTurning * alignSpeed;
			}
		}
	}
	
	void OnControllerColliderHit(ControllerColliderHit hit) {
		Block block = hit.gameObject.GetComponent<Block>();
		if (block != null) {			
			// Other object's normal vector.
			float normal = Mathf.Atan2(hit.normal.x, hit.normal.z)*Mathf.Rad2Deg;
			// Round to 2 decimals.
			normal = Mathf.Round(normal * 10f)/10f;
			normal = Tools.ReduceDegrees(normal);
			// Get reflection.
			float inverted = normal + 180f;
			inverted = Tools.ReduceDegrees(inverted);
			// Average unique normals.
			// Workaround: Unity sometimes returns normals as if the object's position had been inverted. Ignore.
			if (!normals.Contains(normal) && !normals.Contains(inverted)) {
				normals.Add(normal);
				times.Add(Control.RunTime);
			}
			
			// Get tangent to wall.
			float target = Perpendicular(normal)*Mathf.Deg2Rad;
			Vector3 dir = new Vector3(Mathf.Sin(target), 0f, Mathf.Cos(target));
			Vector3 source1 = new Vector3(transform.position.x, transform.position.y - transform.localScale.y, transform.position.z);
			Vector3 source2 = new Vector3(transform.position.x, transform.position.y + transform.localScale.y, transform.position.z);
			Debug.DrawRay(source1, 5f*dir, Color.green, 0.250f);
			Debug.DrawRay(source2, 5f*dir, Color.green, 0.250f);
			
			// If turning to tangent will cause to hit the lastWall within Xcm, compute target as the mean of previous contacts.
			if (Physics.Raycast(source1, dir, 5) || Physics.Raycast(source2, dir, 5))
				normal = GetNormal();
			
			// If normal changed or if rotated too much from current target.
			if (Mathf.Abs(Mathf.DeltaAngle(normal, lastNormal)) > 5f || Delta(Perpendicular(lastNormal)) > 35f) {
				active = true;
				// Mean from all normals alignment.
				lastNormal = normal;
				// Target direction.
				lastTarget = Perpendicular(normal);
				// Shortest turning direction.
				lastTurning = Mathf.Sign(Mathf.DeltaAngle(transform.eulerAngles.y, lastTarget));
			}
			
			// Ignore unless direction of movement and forward direction match within a few degrees.
			// float diff1 = Vector3.Angle(hit.moveDirection, GetComponent<CharacterController>().transform.forward);
			// float diff2 = Vector3.Angle(hit.moveDirection, -GetComponent<CharacterController>().transform.forward); if (diff1 < 45f || diff2 < 45f) {}
		}
	}
	
	float GetNormal() {
		float sin = 0f;
		float cos = 0f;
		float sum = 0f;
		float now = Control.RunTime;
		for (int i = 0; i < normals.Count; i++) {
			// Newer normals have more weight for the computation of the target normal.
			float w = 1f/Mathf.Max(now - times[i], 0.1f);
			sin += w * Mathf.Sin(normals[i] * Mathf.Deg2Rad);
			cos += w * Mathf.Cos(normals[i] * Mathf.Deg2Rad);
			sum += w;
		}
		return Mathf.Atan2(sin/sum, cos/sum)*Mathf.Rad2Deg;
	}
	
	float Delta(float reference) {
		return Delta(reference, transform.eulerAngles.y);
	}
	
	float Delta(float reference, float test) {
		return Mathf.Abs(Mathf.DeltaAngle(reference, test));
	}
	
	// Nearest perpendicular to reference angle.
	float Perpendicular(float reference) {
		float test = transform.eulerAngles.y;
		float change = Mathf.Sign(Mathf.Sin((test - reference) * Mathf.Deg2Rad));
		return reference + 90f*change;
	}
	
	public float Speed {
		get {
			return speed;
		}
	}
}