/* 
 * 2015-09-19. Leonardo Molina.
 * 2019-08-05. Last modification.
 */

/*
	IOS
	http://forum.unity3d.com/threads/trying-to-make-my-own-copy-paste-text-plugin-for-ios-code-included-nothing-happens.117359/
	
	General
	https://github.com/wpp1983/Unity3dMobileClipboardHelper/blob/master/clipboard.cs
	
	Android / Eclipse / jar
	http://unityspain.com/topic/11959-acceder-al-portapapelesclipboard-del-dispositivo/
*/

using UnityEngine;
using System;
using System.Reflection;
using System.Runtime.InteropServices;

#if UNITY_EDITOR
using UnityEditor;

public class Clipboard {
	public static string Text {
		get {
			return EditorGUIUtility.systemCopyBuffer;
		}
		set {
			EditorGUIUtility.systemCopyBuffer = value;
		}
	}
}

#else
	
	public class Clipboard {
		private static PropertyInfo systemCopyBufferProperty = null;
		
		private Clipboard() {
			Type T = typeof(GUIUtility);
			systemCopyBufferProperty = T.GetProperty("systemCopyBuffer", BindingFlags.Static | BindingFlags.NonPublic);
		}

		public static string Text {
			get {
				string text = "";
				try {
					text = (string) systemCopyBufferProperty.GetValue(null, null);
				} catch (Exception e) {
					text = "error copying: " + e.Message;
				}
				return text;
			}
			set {
				systemCopyBufferProperty.SetValue(null, value, null);
			}
		}
	}
	
#endif
