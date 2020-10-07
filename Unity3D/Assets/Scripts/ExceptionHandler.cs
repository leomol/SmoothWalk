/* 
 * 2015-09-19. Leonardo Molina.
 * 2019-08-05. Last modification.
 */

using System;
using System.IO;
using UnityEngine;
 
public class ExceptionHandler : MonoBehaviour {
	public static string filename;

	void OnEnable() {
		Application.logMessageReceivedThreaded += HandleException;
	}
	
	void OnDisable() {
		Application.logMessageReceivedThreaded -= HandleException;
	}

	void HandleException(string logString, string stackTrace, LogType type) {
		if (type == LogType.Exception || type == LogType.Error) {
			try {
				filename = Path.Combine(Control.logsFolder, Control.sessionId + "-errors.txt");
				File.AppendAllText(filename, string.Format("{0}: {1}\n{2}", type, logString, stackTrace));
			} catch {}
		}
	}
}