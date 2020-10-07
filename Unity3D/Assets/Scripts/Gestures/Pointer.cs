/* 
 * 2015-09-19. Leonardo Molina.
 * 2019-08-05. Last modification.
 */

using UnityEngine;

namespace Pointers {
	public class Pointer {
		public int fingerId;
		public float time;
		public float deltaTime;
		public Vector2 position;
		public Vector2 deltaPosition;
		public TouchPhase phase;
		
		public Pointer(float time, Touch touch) {
			this.fingerId = touch.fingerId;
			this.time = time;
			this.deltaTime = touch.deltaTime;
			this.position = touch.position;
			this.deltaPosition = touch.deltaPosition;
			this.phase = touch.phase;
		}
		
		public Pointer() {
			fingerId = 0;
			time = 0f;
			deltaTime = 0f;
			position = Vector2.zero;
			deltaPosition = Vector2.zero;
		}
		
		public Pointer ShallowCopy() {
		   Pointer pointer = (Pointer) this.MemberwiseClone();
		   return pointer;
		}
	}
}