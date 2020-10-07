/* 
 * 2015-09-19. Leonardo Molina.
 * 2019-08-05. Last modification.
 */

using System;
using System.Collections;
using UnityEngine;

public class AndroidBridge : MonoBehaviour, IBridge {
	public event InputHandler InputChanged;
	public event ConnectionHandler ConnectionChanged;
	
	public void Setup(int baudrate) {
	}
	
	public void Start() {
	}
	
	public void Write(byte[] output) {
	}
	
	public void Dispose() {
		GameObject.Destroy(this.gameObject);
	}
}