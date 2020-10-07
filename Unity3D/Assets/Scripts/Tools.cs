/* 
 * External libraries and general use functions.
 * 2015-12-18. Leonardo Molina.
 * 2019-03-21. Last modification.
 */

// http://stackoverflow.com/questions/6334283/declspec-and-stdcall-vs-declspec-only
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;
using Microsoft.Win32;

public static class Tools {
	// string OS = SystemInfo.operatingSystem;
	// Match match = Regex.Match(OS, @"Windows (8\.?|10).*");
	
	public static bool IsWindows {
		get {
			return Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor;
		}
	}
	
	public static bool IsAndroid {
		get {
			#if UNITY_ANDROID && !UNITY_EDITOR
				return true;
			#else
				return false;
			#endif
		}
	}
	
	public static int Dice(float[] probabilities) {
		int result = 0;
		int np = probabilities.Length;
		for (int p = 1; p < np; p++)
			probabilities[p] += probabilities[p-1];
		float r = probabilities[np - 1]*UnityEngine.Random.value;
		for (int p = 0; p < np; p++) {
			if (r < probabilities[p]) {
				result = p;
				break;
			}
		}
		return result;
	}
	
	public static bool Tone(float frequency, float duration) {
		bool success = !Global.AudioSource.isPlaying && frequency > 0 && duration > 0;
		if (success) {
			float sr = AudioSettings.outputSampleRate;
			float step = 2*Mathf.PI*frequency/sr;
			int n = (int) Mathf.Round(duration*sr);
			float[] wave = new float[n];
			for (int i = 0; i < n; i++)
				wave[i] = Mathf.Sin(i*step);
			AudioClip codeClip = AudioClip.Create("Wave", n, 1, AudioSettings.outputSampleRate, false);
			codeClip.SetData(wave, 0);
			Global.AudioSource.clip = codeClip;
			Global.AudioSource.Play();
		}
		return success;
	}
	
	public static float ReduceDegrees(float angle) {
		return ((angle % 360f) + 360f) % 360f;
	}
	
	public static float MeanRadians(List<float> angles) {
		float sin = 0f;
		float cos = 0f;
		foreach (float angle in angles) {
			sin += Mathf.Sin(angle);
			cos += Mathf.Cos(angle);
		}
		return Mathf.Atan2(sin/angles.Count, cos/angles.Count);
	}
	
	public static float MeanDegrees(List<float> angles) {
		float sin = 0f;
		float cos = 0f;
		foreach (float angle in angles) {
			sin += Mathf.Sin(angle * Mathf.Deg2Rad);
			cos += Mathf.Cos(angle * Mathf.Deg2Rad);
		}
		return Mathf.Atan2(sin/angles.Count, cos/angles.Count)*Mathf.Rad2Deg;
	}
	
	public static float VarRadians(List<float> angles) {
		float mean = MeanRadians(angles);
		float sin2 = 0f;
		float cos2 = 0f;
		foreach (float angle in angles) {
			float diffSquared = (angle - mean) * (angle - mean);
			sin2 += Mathf.Sin(diffSquared);
			cos2 += Mathf.Cos(diffSquared);
		}
		return Mathf.Atan2(sin2,cos2);
	}
	
	public static float VarDegrees(List<float> angles) {
		float mean = MeanDegrees(angles) * Mathf.Deg2Rad;
		float sin2 = 0f;
		float cos2 = 0f;
		foreach (float angle in angles) {
			float diffSquared = (angle * Mathf.Deg2Rad - mean) * (angle * Mathf.Deg2Rad - mean);
			sin2 += Mathf.Sin(diffSquared);
			cos2 += Mathf.Cos(diffSquared);
		}
		return Mathf.Atan2(sin2,cos2)*Mathf.Rad2Deg;
	}
	
	public static Vector2 RotateDegrees(Vector2 v, float angle) {
		angle *= Mathf.Deg2Rad;
		float sin = Mathf.Sin(angle);
		float cos = Mathf.Cos(angle);
		return new Vector2(v.x*cos - v.y*sin, v.x*sin + v.y*cos);
	}
	
	// Right-handed rotation z to Unity's rotation y and vice-versa.
	public static float Mirror(float r) {
		return Tools.ReduceDegrees(90f - r);
	}
	
	public static Vector2 FlipAxis(Vector2 v) {
		return new Vector2(v.y, v.x);
	}
	
	public static float Slope(List<float> x, List<float> y) {
		float xMean = 0f;
		float yMean = 0f;
		int n = x.Count;
		for (int i = 0; i < n; i++) {
			xMean += x[i];
			yMean += y[i];
		}
		xMean /= n;
		yMean /= n;
		float deviationXY = 0f;
		float deviationXX = 0f;
		for (int i = 0; i < n; i++) {
			float xDeviation = (x[i] - xMean);
			float yDeviation = (y[i] - yMean);
			deviationXY += xDeviation * yDeviation;
			deviationXX += xDeviation * xDeviation;
		}
		return deviationXY/deviationXX;
	}
	
	public static float FitSlope(List<float> x, List<float> y, int nsteps) {
		int best = 0;
		double vmax = double.NegativeInfinity;
		double step = Math.PI/nsteps;
		double cos = Math.Cos(step);
		double sin = Math.Sin(step);
		
		int n = x.Count;
		for (int s = 1; s <= nsteps; s++) {
			double vs = 0d;
			for (int i = 0; i < n; i++) {
				double xi = x[i]*cos + y[i]*sin;
				double yi = y[i]*cos - x[i]*sin;
				x[i] = (float) xi;
				y[i] = (float) yi;
				vs += Math.Abs(xi);
			}
			if (vs > vmax) {
				vmax = vs;
				best = s;
			}
		}
		return (float) (best*step);
	}
	
	/*
		string parsing = "a;;a ;b;c; ";
		string parsed = "x";
		bool ok = false;
		ok = Collect(ref parsing, ref parsed); // ok << true, parsing << "b;c; ", parsed << "a;;a "
		ok = Collect(ref parsing, ref parsed); // ok << true, parsing << "c; ", parsed << "b"
		ok = Collect(ref parsing, ref parsed); // ok << true, parsing << " ", parsed << "c"
		ok = Collect(ref parsing, ref parsed); // ok << false, parsing << " ", parsed << "c"
	*/
	public static bool Collect(ref string parsing, ref string parsed) {
		Match match = Regex.Match(parsing, @"(?<!;);(?!;)");
		bool success = match.Success;
		if (success) {
			int p = match.Index;
			parsed = parsing.Substring(0, p);
			if (p < parsing.Length)
				parsing = parsing.Substring(p+1);
			else
				parsing = "";
		}
		return success;
	}
	
	public static string Collect(ref string parsing) {
		string parsed = "";
		Collect(ref parsing, ref parsed);
		return parsed;
	}
	
	/*
		string parsing = " a,b ";
		string parsed = " c ";
		bool ok = false;
		ok = Parse(ref parsing, ref parsed); // ok << true, parsing << "b ", parsed << "a"
		ok = Parse(ref parsing, ref parsed); // ok << true, parsing << "", parsed << "b"
		ok = Parse(ref parsing, ref parsed); // ok << false, parsing << "", parsed << "b"
	*/
	public static bool Parse(ref string parsing, ref string parsed) {
		int p = parsing.IndexOf(',');
		bool success = true;
		if (p > -1) {
			parsed = parsing.Substring(0, p);
			if (p < parsing.Length)
				parsing = parsing.Substring(p+1);
			else
				parsing = "";
		} else if (parsing.Length > 0) {
			parsed = parsing;
			parsing = "";
		} else {
			success = false;
		}
		return success;
	}
	
	/* 
		string parsing = " a,b ";
		string parsed = "";
		parsed = Parse(ref parsing); // parsing << "b " , parsed << " a"
		parsed = Parse(ref parsing); // parsing << "", parsed << "b "
		parsed = Parse(ref parsing); // parsing << "", parsed << ""
	*/
	public static string Parse(ref string parsing) {
		string parsed = "";
		Parse(ref parsing, ref parsed);
		return parsed;
	}
	
	public static bool Scan(string instructions, ref Dictionary<string, string> parameters, params string[] keys) {
		Dictionary<string, string> existing = new Dictionary<string, string>();
		string instruction = "";
		while (Collect(ref instructions, ref instruction))
			existing[Parse(ref instruction)] = Parse(ref instruction);
		bool success = true;
		foreach (string key in keys) {
			if (existing.ContainsKey(key))
				parameters[key] = existing[key];
			else
				success = false;
		}
		return success;
	}
	
	public static bool ParseRange(string text, ref float[] range) {
		bool success = false;
		float[] floats = new float[0];
		float[] values = new float[2];
		if (ParseFloats(text, ref floats)) {
			int n = floats.Length;
			if (n == 1) {
				values[0] = floats[0];
				values[1] = values[0];
				success = true;
			} else if (n == 2) {
				values[0] = floats[0];
				values[1] = floats[1];
				success = values[1] >= values[0];
			}
		}
		if (success)
			range = values;
		return success;
	}
	
	public static bool ParseFloats(string text, ref float[] floats) {
		bool success = true;
		text = text.Trim();
		string[] parts = Regex.Split(text, @",");
		int n = parts.Length;
		float[] values = new float[n];
		for (int i = 0; i < n; i++) {
			if (!float.TryParse(parts[i], out values[i])) {
				success = false;
				break;
			}
		}
		if (success)
			floats = values;
		return success;
	}
	
	public static string Trim(string text) {
		return Regex.Replace(text, @"\s{2,}", " ").Trim();
	}
	
	// Split by commas, except things inside brackets [] or {}
	public static string[] Split(string text) {
		// text = Regex.Replace(text, @"(\]|}),?", @"$1,");
		// text = Regex.Replace(text, @",?(\[|{)", @",$1");
		return Regex.Split(text, @",(?![^{]*})(?![^\[]*\])");
	}
	
	public static string Escape(string input) {
		return input.Replace(";", ";;");
	}
	
	public static string UnEscape(string input) {
		return input.Replace(";;", ";");
	}
	
	public static void Body(string url, Action<bool, string> callback) {
		Thread thread;
		thread = new Thread(() => BodyThread(url, callback));
		thread.IsBackground = true;
		thread.Start();
	}
	
	public static void BodyThread(string url, Action<bool, string> callback) {
		string response = "";
		bool success = false;
		try {
			HttpWebRequest webRequest = (HttpWebRequest) WebRequest.Create(url);
			webRequest.Timeout = 3000;
			webRequest.UserAgent = "SmoothWalk App";
			webRequest.ContentType = "application/x-www-form-urlencoded";
			webRequest.AllowAutoRedirect = true;
			webRequest.Method = "GET";
			WebResponse webResponse = webRequest.GetResponse();
			Stream dataStream = webResponse.GetResponseStream();
			StreamReader reader = new StreamReader(dataStream);
			response += reader.ReadToEnd();
			reader.Close();
			webResponse.Close();
			success = true;
		} catch (Exception e) {
			response = e.Message;
		}
		callback(success, response);
	}
	
	public static void Headers(string url, Action<bool, WebHeaderCollection> callback) {
		Thread thread;
		thread = new Thread(() => HeadersThread(url, callback));
		thread.IsBackground = true;
		thread.Start();
	}
	
	public static void HeadersThread(string url, Action<bool, WebHeaderCollection> callback) {
		WebHeaderCollection headers = new WebHeaderCollection();
		bool success = false;
		try {
			HttpWebRequest webRequest = (HttpWebRequest) WebRequest.Create(url);
			webRequest.Timeout = 3000;
			webRequest.UserAgent = "SmoothWalk App";
			webRequest.ContentType = "application/x-www-form-urlencoded";
			webRequest.AllowAutoRedirect = true;
			webRequest.Method = "GET";
			WebResponse webResponse = webRequest.GetResponse();
			headers = webResponse.Headers;
			webResponse.Close();
			success = true;
		} catch {}
		callback(success, headers);
	}
	
	public static void Visible(bool visible, params Selectable[] list) {
		foreach (Selectable item in list)
			item.gameObject.SetActive(visible);
	}
	
	public static void Visible(bool visible, params GameObject[] list) {
		foreach (GameObject item in list)
			item.SetActive(visible);
	}
	
	public static void CopyMaterial(GameObject source, params GameObject[] targets) {
		Material srcMaterial = source.GetComponent<Renderer>().material;
		Vector2 srcScale = GetTextureScale(source);
		foreach (GameObject target in targets) {
			target.GetComponent<Renderer>().material = srcMaterial;
			SetTextureScale(srcScale, target);
		}
	}
	
	public static Vector2 GetTextureScale(GameObject source) {
		Material srcMaterial = source.GetComponent<Renderer>().material;
		Vector2 srcScale = source.transform.lossyScale;
		Vector2 texScale = srcMaterial.mainTextureScale;
		Vector2 scale = new Vector2(texScale.x/srcScale.x, texScale.y/srcScale.y);
		return scale;
	}
	
	public static void SetTextureScale(Vector2 scale, params GameObject[] objects) {
		foreach (GameObject obj in objects) {
			Vector2 size = obj.transform.lossyScale;
			Material material = obj.GetComponent<Renderer>().material;
			material.SetTextureScale("_MainTex", new Vector2(scale.x*size.x, scale.y*size.y));
			material.SetTextureScale("_BumpMap", new Vector2(scale.x*size.x, scale.y*size.y));
		}
	}
	
#if UNITY_ANDROID
	public static bool KeyboardAvailable {
		get {
			return false;
		}
	}
	
	public static void ShowOnScreenKeyboard() {
	}
	
	public static void HideOnScreenKeyboard() {
	}
	
#else
	public static bool KeyboardAvailable {
		get {
			bool available = FindWindow("IPTip_Main_Window", null) != IntPtr.Zero;
			return available;
		}
	}
	
	static bool DoesWin32MethodExist(string moduleName, string methodName) {
		IntPtr moduleHandle = GetModuleHandle(moduleName);
		if (moduleHandle == IntPtr.Zero)
		{
			return false;
		}
		return (GetProcAddress(moduleHandle, methodName) != IntPtr.Zero);
	}
	
	static bool is64 = false;
	static bool is64Once = true;
	static bool Is64BitOS() {
		if (is64Once) {
			if (IntPtr.Size == 8) {
				is64 = true;
			} else {
				bool flag;
				is64 = ((DoesWin32MethodExist("kernel32.dll", "IsWow64Process") &&
					IsWow64Process(GetCurrentProcess(), out flag)) && flag);
			}
			is64Once = false;
		}
		return is64;
	}
	
	[DllImport("kernel32.dll")]
	static extern IntPtr GetCurrentProcess();

	[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
	static extern IntPtr GetModuleHandle(string moduleName);

	[DllImport("kernel32", CharSet = CharSet.Auto, SetLastError = true)]
	static extern IntPtr GetProcAddress(IntPtr hModule,
		[MarshalAs(UnmanagedType.LPStr)]string procName);

	[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	static extern bool IsWow64Process(IntPtr hProcess, out bool wow64Process);
	
	public static void ShowOnScreenKeyboard() {
		if (KeyboardAvailable) {
			// Hide first so that the simulated click shows it.
			HideOnScreenKeyboard();
			IntPtr parent = FindWindow("Shell_TrayWnd", null);
			IntPtr child1 = FindWindowEx(parent, IntPtr.Zero, "TrayNotifyWnd", "");
			IntPtr keyboardWnd = FindWindowEx(child1, IntPtr.Zero, null, "Touch keyboard");

			uint WM_LBUTTONDOWN = 0x0201;
			uint WM_LBUTTONUP = 0x0202;
			UIntPtr x = new UIntPtr(0x01);
			UIntPtr x1 = new UIntPtr(0);
			IntPtr y = new IntPtr(0x0240012);
			PostMessage(keyboardWnd, WM_LBUTTONDOWN, x, y);
			PostMessage(keyboardWnd, WM_LBUTTONUP, x1, y);
		}
	}

	public static void HideOnScreenKeyboard() {
		uint WM_SYSCOMMAND = 0x0112;
		UIntPtr SC_CLOSE = new UIntPtr(0xF060);
		IntPtr y = new IntPtr(0);
		IntPtr KeyboardWnd = FindWindow("IPTip_Main_Window", null);
		PostMessage(KeyboardWnd, WM_SYSCOMMAND, SC_CLOSE, y);
	}
	
	[return: MarshalAs(UnmanagedType.Bool)]
	[DllImport("user32.dll", SetLastError = true)]
	static extern bool PostMessage(IntPtr hWnd, uint Msg, UIntPtr wParam, IntPtr lParam);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	static extern IntPtr FindWindow(String sClassName, String sAppName);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, String lpszClass, String lpszWindow);
#endif
}