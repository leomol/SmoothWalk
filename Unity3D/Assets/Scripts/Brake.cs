/* Temporal dampSpeed.
 * 
 * 2015-02-08. Leonardo Molina.
 * 2016-02-12. Last modification.
 */
 
using System;
using UnityEngine;

public class Brake {
	float[] dampSpeed = new float[3]{0f, 0f, 0f};
	float[] step = new float[3]{0f, 0f, 0f};
	public float Duration = 0f;
	
	public Brake(float duration) {
		Duration = duration;
	}
	
	public Brake() {
	}
	
	public void Update(ref Vector3 speedVector, float dt) {
		float[] speed = new float[3]{speedVector.x, speedVector.y, speedVector.z};
		for (int i = 0; i < 3; i++) {
			if (Duration > 0f) {
				// If the speed changes sign, or the speed increases in magnitude, or damping reached "zero".
				if (dampSpeed[i] * speed[i] < 0 || Mathf.Abs(speed[i]) >= Mathf.Abs(dampSpeed[i]) || Mathf.Abs(step[i]) > Mathf.Abs(dampSpeed[i])) {
					// Initialize.
					dampSpeed[i] = speed[i];
					step[i] = speed[i]/Duration*dt;
				} else {
					// Damp.
					dampSpeed[i] -= step[i];
					speed[i] = dampSpeed[i];
				}
			}
		}
		speedVector = new Vector3(speed[0], speed[1], speed[2]);
	}
	
	public void Reset() {
		for (int i = 0; i < 3; i++)
			dampSpeed[i] = 0f;
	}
}