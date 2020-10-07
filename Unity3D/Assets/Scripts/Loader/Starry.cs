/*
	2017-06-05. Leonardo Molina.
	2017-08-24. Last modification.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Random = System.Random;

namespace Interphaser.Loader {
	public class Starry : MonoBehaviour {
		int tileNi, tileNj, dotNi, dotNj, marginNi, marginNj, ni, nj;
		float repeatKi, repeatKj, ratio, interval;
		
		static Random random = new Random(0);
		static Random randomBlink = new Random(1);
		Material material;
		Coroutine blink;
		bool ready = false;
		Texture2D texture;
		
		void Awake() {
			material = new Material(Shader.Find("Unlit/Texture"));
			#if UNITY_EDITOR
			// Create an asset once so that the shader is included during compilation.
			string target = "Assets/Resources/UnlitMaterial";
			string materialTarget = string.Format("{0}.mat", target);
			// Save texture and material, otherwise prefabs build from these won't load these components.
			AssetDatabase.CreateAsset(material, materialTarget);
			#endif
			gameObject.GetComponent<Renderer>().material = material;
			ready = true;
		}
		
		// Size of tile, size of dot shape, margin, number of tiles, repetitions, on-probability, update frequency.
		public void Setup(int tileNi, int tileNj, int dotNi, int dotNj, int marginNi, int marginNj, int ni, int nj, float repeatKi, float repeatKj, float ratio, float interval) {
			this.tileNi = tileNi;
			this.tileNj = tileNj;
			this.dotNi = dotNi;
			this.dotNj = dotNj;
			this.marginNi = marginNi;
			this.marginNj = marginNj;
			this.ni = ni;
			this.nj = nj;
			this.repeatKi = repeatKi;
			this.repeatKj = repeatKj;
			this.ratio = ratio;
			this.interval = interval;
			
			StartCoroutine(SetupCoroutine());
		}
		
		IEnumerator SetupCoroutine() {
			yield return new WaitUntil(() => ready);
			if (blink != null)
				StopCoroutine(blink);
			if (ratio == 1f || ratio == 0f || interval == 0f)
				NewTexture(tileNi, tileNj, dotNi, dotNj, marginNi, marginNj, ni, nj, repeatKi, repeatKj, ratio);
			else
				blink = StartCoroutine(Blink());
		}
		
		void NewTexture(int tileNi, int tileNj, int dotNi, int dotNj, int marginNi, int marginNj, int ni, int nj, float repeatKi, float repeatKj, float ratio) {
			ratio = 1f - ratio;
			
			int canvasNi = ni * tileNi;
			int canvasNj = nj * tileNj;
			
			Color[] canvas = new Color[canvasNi * canvasNj];
			int di = tileNi - 2 * marginNi - dotNi;
			int dj = tileNj - 2 * marginNj - dotNj;
			for (int i = 0; i < canvasNi; i += tileNi) {
				for (int j = 0; j < canvasNj; j += tileNj) {
					int ri = random.Next(di + 1) + marginNi;
					int rj = random.Next(dj + 1) + marginNj;
					if (randomBlink.NextDouble() >= ratio) {
						for (int ii = ri; ii < ri + dotNi; ii++) {
							for (int jj = rj; jj < rj + dotNj; jj++) {
								// int kk = (j + jj) + (i + ii) * canvasNj;
								int kk = (i + ii) + (j + jj) * canvasNi;
								canvas[kk] = Color.white;
							}
						}
					}
				}
			}
			
			Destroy(texture);
			texture = new Texture2D(canvasNi, canvasNj, TextureFormat.RGBA32, false);
			texture.SetPixels(canvas);
			texture.Apply();
			texture.filterMode = FilterMode.Point;
			texture.wrapMode = TextureWrapMode.Repeat;
			material.mainTexture = texture;
			material.mainTextureScale = new Vector2(repeatKi, repeatKj);
		}
		
		IEnumerator Blink() {
			while (true) {
				NewTexture(tileNi, tileNj, dotNi, dotNj, marginNi, marginNj, ni, nj, repeatKi, repeatKj, ratio);
				yield return new WaitForSeconds(interval);
			}
		}
		
		// Update is called once per frame
		void Update() {
		}
		
		void OnDestroy() {
			Destroy(texture);
		}
	}
}