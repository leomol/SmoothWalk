/* 
 * 2015-09-19. Leonardo Molina.
 * 2019-08-05. Last modification.
 */

using System;
using System.IO;
using System.Threading;
using UnityEngine;

public class Logger : MonoBehaviour {
	int caret = 0;
	static bool mutexIdle = true;
	string buffer = "";
	readonly object locker = new object();
	
	void OnApplicationQuit() {
		mutexIdle = true;
	}
	
	public void Log(string str) {
		lock (locker)
			buffer += str + "\n";
	}
	
	public bool Append(Action<bool, string> callback, string filename) {
		// Book mechanism until part of buffer is saved.
		if (mutexIdle) {
			mutexIdle = false;
			lock (locker) {
				if (buffer.Length > 0) {
					// Capture buffer size.
					caret = buffer.Length;
					// Try saving once on a different thread, no re-attempts.
					Thread appendThread;
					appendThread = new Thread(() => AppendThread(filename, buffer, callback));
					appendThread.IsBackground = true;
					appendThread.Start();
				}
			}
			return true;
		} else {
			return false;
		}
	}
	
	void AppendThread(string filename, string text, Action<bool, string> callback) {
		try {
			File.AppendAllText(filename, text);
			// Remove part of the buffer that was sent.
			lock (locker)
				buffer = buffer.Substring(caret);
			mutexIdle = true;
			callback(true, "");
		} catch (Exception e) {
			mutexIdle = true;
			callback(false, e.Message);
		}
	}
}