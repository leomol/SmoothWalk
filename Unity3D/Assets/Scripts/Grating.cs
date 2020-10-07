/* 
 * 2015-09-19. Leonardo Molina.
 * 2019-08-05. Last modification.
 */

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;
using System.Threading;

public class Grating : MonoBehaviour {
	// Share dummy camera and object.
	Camera dummyC;
	GameObject dummyO;
	float lastDistance = Mathf.Infinity;
	bool busy = true;
	Texture2D texture;
	Renderer targetR;
	
	void Start() {
		dummyO = new GameObject("DummyCamera");
		dummyO.AddComponent<Camera>();
		dummyC = dummyO.GetComponent<Camera>();
		dummyC.enabled = false;
		dummyO.transform.parent = this.gameObject.transform;
		targetR = GetComponent<Renderer>();
		busy = false;
	}
	
	float Distance {
		get {
			return float.Parse(Global.Control.Get("monitorDistance"));
		}
	}
	
	Camera CurrentCamera {
		get {
			return Global.Control.CurrentCamera;
		}
	}
	
	public void SetCyclesPerDegree(float cpd, float theta, float phase, int limit) {
		StartCoroutine(CyclesPerDegree(cpd, theta, phase, limit, IsHorizontal(theta) || IsVertical(theta) ? 0f : 1f));
	}
	
	// Cycles per degree remain constant, within step.
	IEnumerator CyclesPerDegree(float cpd, float theta, float phase, int limit, float step) {
		while (busy)
			yield return null;
		
		while (true) {
			Plane plane = new Plane(transform.forward, transform.position);
			float distance = Mathf.Abs(plane.GetDistanceToPoint(CurrentCamera.transform.position));
			dummyO.transform.position = transform.position - distance*transform.forward;
			if (Mathf.Abs(distance - lastDistance) > step) {
				lastDistance = distance;
				
				Vector3 center = transform.position;
				float dx = 0.5f*transform.lossyScale.x;
				float dy = 0.5f*transform.lossyScale.y;
				dummyO.transform.LookAt(transform);
				dummyC.fieldOfView = CurrentCamera.fieldOfView;
				dummyC.projectionMatrix = CurrentCamera.projectionMatrix;
				yield return new WaitForEndOfFrame();
				
				Vector2 r = dummyC.WorldToScreenPoint(center + dx * transform.right);
				Vector2 l = dummyC.WorldToScreenPoint(center - dx * transform.right);
				Vector2 t = dummyC.WorldToScreenPoint(center + dy * transform.up);
				Vector2 b = dummyC.WorldToScreenPoint(center - dy * transform.up);
				
				// Debug.DrawLine(center, center + dx * transform.right, Color.red, 100f);
				// Debug.DrawLine(center, center - dx * transform.right, Color.blue, 100f);
				// Debug.DrawLine(center, center + dy * transform.up, Color.green, 100f);
				// Debug.DrawLine(center, center - dy * transform.up, Color.yellow, 100f);
				
				// mi and mj will tend to infinity (hence overflow) as the camera and object become closer.
				float w = (r - l).magnitude;
				float h = (t - b).magnitude;
				int mj = limit;
				int mi = limit;
				if (w < limit)
					mj = (int) w;
				if (h < limit)
					mi = (int) h;
				
				float width = Screen.width/Screen.dpi*2.54f;
				float fov = Mathf.Atan(width/Distance);
				// Pixels per degree.
				float ppd = Screen.width/fov*Mathf.Deg2Rad;
				// Wavelength (number of pixels per cycle).
				float ppc = ppd/cpd;
				StartCoroutine(SetGrating(ppc, theta, phase, mi, mj, limit));
			}
			yield return new WaitForEndOfFrame();
		}
	}
	
	// Number of cycles remain constant.
	public void SetCycles(int ncycles, float ppc, float theta, float phase, float aspect) {
		StartCoroutine(Cycles(ncycles, ppc, theta, phase, aspect));
	}
	
	IEnumerator Cycles(int ncycles, float ppc, float theta, float phase, float aspect) {
		while (busy)
			yield return null;
		
		int mi = Mathf.RoundToInt(ncycles * ppc * aspect);
		int mj = (int) (ncycles * ppc);
		StartCoroutine(SetGrating(ppc, theta, phase, mi, mj, mj));
	}
	
	static bool IsHorizontal(float theta) {
		return Mathf.Abs(Mathf.Cos(theta)) < 1e-5f;
	}
	
	static bool IsVertical(float theta) {
		return Mathf.Abs(Mathf.Sin(theta)) < 1e-5f;
	}
	
	IEnumerator SetGrating(float ppc, float theta, float phase, int mi, int mj, int limit) {
		if (mutex.WaitOne()) {
			float cost = 1f;
			float sint = 1f;
			int ni = 1;
			int nj = 1;
			int nr = Mathf.RoundToInt(ppc);
			if (IsHorizontal(theta)) {
				cost = 0f;
				ni = Mathf.RoundToInt(ppc);
				mj = 1;
			} else if (IsVertical(theta)) {
				sint = 0f;
				nj = Mathf.RoundToInt(ppc);
				mi = 1;
			} else {
				cost = Mathf.Cos(theta);
				sint = Mathf.Sin(theta);
				ni = Math.Abs(Mathf.RoundToInt(ppc/sint));
				nj = Math.Abs(Mathf.RoundToInt(ppc/cost));
				// Smallest from maximum size allowed, tile size, texture size.
				ni = (int) Mathf.Min(ni, mi, limit);
				nj = (int) Mathf.Min(nj, mj, limit);
				nr = nj;
			}
			
			float freq = nr/ppc;
			int ti = Math.Max(ni - 1, 1);
			int tj = Math.Max(nj - 1, 1);
			
			// Compute proportion for a given orientation.
			float[] x = new float[nj];
			float[] y = new float[ni];
			for (int j = 0; j < nj; j++) {
				float v = (j - 1f)/(nr - 1f) - 0.5f;
				x[j] = v * cost;
			}
			for (int i = 0; i < ni; i++) {
				float v = (i - 1f)/(nr - 1f) - 0.5f;
				y[i] = v * sint;
			}
			
			producing = true;
			Thread setThread = new Thread(() => MakeGratingThread(x, y, ti, tj, freq, phase));
			setThread.IsBackground = true;
			setThread.Start();
			while (producing)
				yield return null;
			
			if (texture != null)
				Destroy(texture);
			texture = new Texture2D(ti, tj, TextureFormat.RGB24, false);
			// SetPixels is faster than SetPixel.
			texture.SetPixels(cols);
			//texture.Compress(false);
			targetR.material.mainTexture = texture;
			targetR.material.mainTextureScale = new Vector2(mi / (float) ni, mj / (float) nj);
			texture.Apply(false);
			
			mutex.ReleaseMutex();
		}
	}
	
	Mutex mutex = new Mutex();
	volatile bool producing = false;
	Color[] cols;
	void MakeGratingThread(float[] x, float[] y, int ti, int tj, float freq, float phase) {
		cols = new Color[ti*tj];
		// Otherwise stick to horizontal or vertical grids.
		for (int i = 0; i < ti; i++) {
			for (int j = 0; j < tj; j++) {
				// Convert to radians and scale by frequency.
				float v = Mathf.Sin((x[j] + y[i]) * freq * 2f*Mathf.PI + phase);
				// Make 2D sinewave.
				cols[j*ti + i].r = v;
				cols[j*ti + i].g = v;
				cols[j*ti + i].b = v;
			}
		}
		producing = false;
	}
	
	void OnApplicationQuit() {
		mutex.Close();
	}
}
