/* 
 * 2015-09-19. Leonardo Molina.
 * 2019-08-05. Last modification.
 */

using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

public class BundleLoader {
	public delegate void SuccessHandler(string bundlePath, string assetPath, GameObject asset, object data);
	public delegate void FailHandler(string bundlePath, string assetPath, string message, object data);
	public event SuccessHandler Success;
	public event FailHandler Fail;
	
	GameObject gameObject;
	Component component;
	BundleLoaderMono loader;
	
	public BundleLoader() {
		gameObject = new GameObject();
		gameObject.name = "BundleLoader";
		component = gameObject.AddComponent<BundleLoaderMono>();
		loader = (BundleLoaderMono) component;
		loader.Success += OnSuccess;
		loader.Fail += OnFail;
	}
	
	void OnSuccess(string bundlePath, string assetPath, GameObject asset, object data) {
		if (Success != null)
			Success(bundlePath, assetPath, asset, data);
	}
	
	void OnFail(string bundlePath, string assetPath, string message, object data) {
		if (Fail != null)
			Fail(bundlePath, assetPath, message, data);
	}
	
	public void Load(string bundlePath, string assetPath, object data) {
		bundlePath = LoaderTools.NormalizeSeparator(bundlePath);
		assetPath = LoaderTools.NormalizeSeparator(assetPath);
		loader.Load(bundlePath, assetPath, data);
	}
	
	public void Unload(string bundlePath) {
		loader.Unload(LoaderTools.NormalizeSeparator(bundlePath));
	}
}

class BundleLoaderMono : MonoBehaviour {
	public delegate void SuccessHandler(string bundlePath, string assetPath, GameObject asset, object data);
	public delegate void FailHandler(string bundlePath, string assetPath, string message, object data);
	public event SuccessHandler Success;
	public event FailHandler Fail;
	
	readonly object testLock = new object();
	Dictionary<string, bool> locks = new Dictionary<string, bool>();
	Dictionary<string, AssetBundle> bundles = new Dictionary<string, AssetBundle>();
	Dictionary<string, Dictionary<string, GameObject>> assets = new Dictionary<string, Dictionary<string, GameObject>>();
	
	public void Load(string bundlePath, string assetPath, object data) {
		StartCoroutine(AsyncLoad(bundlePath, assetPath, data));
	}
	
	IEnumerator AsyncLoad(string bundlePath, string assetPath, object data) {
		assetPath = AssetName(assetPath);
		bundlePath = AssetName(bundlePath);
		
		lock (testLock) {
			if (locks.ContainsKey(bundlePath))
				yield return new WaitWhile(() => locks[bundlePath]);
			locks[bundlePath] = true;
		}
		
		string message = "";
		bool error = false;
		
		if (!bundles.ContainsKey(bundlePath)) {
			WWW www = new WWW("file://" + bundlePath);
			yield return www;
			if (www.error == null) {
				bundles[bundlePath] = www.assetBundle;
			} else {
				error = true;
				message = "Loader: " + www.error;
			}
			www.Dispose();
		}
		
		if (!error) {
			if (bundles[bundlePath].Contains(assetPath)) {
				if (!assets.ContainsKey(bundlePath))
					assets[bundlePath] = new Dictionary<string, GameObject>();
				if (!assets[bundlePath].ContainsKey(assetPath))
					bundles[bundlePath].LoadAssetWithSubAssets(assetPath);
					assets[bundlePath][assetPath] = bundles[bundlePath].LoadAsset(assetPath) as GameObject;
				if (Success != null)
					Success(bundlePath, assetPath, Instantiate(assets[bundlePath][assetPath]), data);
			} else {
				error = true;
				message = "\"" + assetPath + " does not exist in \"" + bundlePath + "\".";
			}
		}
		if (error)
			if (Fail != null)
				Fail(bundlePath, assetPath, message, data);
		
		locks[bundlePath] = false;
	}
	
	public void Unload(string bundlePath) {
		bundlePath = AssetName(bundlePath);
		if (bundles.ContainsKey(bundlePath)) {
			bundles[bundlePath].Unload(false);
			assets[bundlePath].Clear();
		}
	}
	
	// Unique name for each file.
	string AssetName(string name) {
		name = LoaderTools.ForwardSlash(name).Trim();
		#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
		name = name.ToLower();
		#endif
		return name;
	}
}

public class TextureLoader {
	public delegate void SuccessHandler(string imagePath, Texture2D texture, object data);
	public delegate void FailHandler(string imagePath, string message, object data);
	public event SuccessHandler Success;
	public event FailHandler Fail;
	
	GameObject gameObject;
	Component component;
	TextureLoaderMono loader;
	
	public TextureLoader() {
		gameObject = new GameObject();
		gameObject.name = "TextureLoader";
		component = gameObject.AddComponent<TextureLoaderMono>();
		loader = (TextureLoaderMono) component;
		loader.Success += OnSuccess;
		loader.Fail += OnFail;
	}
	
	void OnSuccess(string imagePath, Texture2D texture, object data) {
		if (Success != null)
			Success(imagePath, texture, data);
	}
	
	void OnFail(string imagePath, string message, object data) {
		if (Fail != null)
			Fail(imagePath, message, data);
	}
	
	public void Load(string imagePath, object data) {
		loader.Load(LoaderTools.NormalizeSeparator(imagePath), data);
	}
	
	public void LoadResource(string imagePath, object data) {
		loader.LoadResource(LoaderTools.NormalizeSeparator(imagePath), data);
	}
	
	public void Unload(string imagePath) {
		loader.Unload(imagePath);
	}
}

class TextureLoaderMono : MonoBehaviour {
	public delegate void SuccessHandler(string imagePath, Texture2D texture, object data);
	public delegate void FailHandler(string imagePath, string message, object data);
	public event SuccessHandler Success;
	public event FailHandler Fail;
	
	readonly object testLock = new object();
	Dictionary<string, bool> locks = new Dictionary<string, bool>();
	Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
	
	public void Load(string imagePath, object data) {
		StartCoroutine(AsyncLoad(imagePath, data));
	}
	
	IEnumerator AsyncLoad(string imagePath, object data) {
		imagePath = AssetName(imagePath);
		lock (testLock) {
			if (locks.ContainsKey(imagePath))
				yield return new WaitWhile(() => locks[imagePath]);
			locks[imagePath] = true;
		}
		
		string message = "";
		bool error = false;
		if (!textures.ContainsKey(imagePath)) {
			WWW www = new WWW("file://" + imagePath);
			yield return www;
			if (www.error == null) {
				textures[imagePath] = new Texture2D(www.texture.width, www.texture.height);
				www.LoadImageIntoTexture(textures[imagePath]);
			} else {
				error = true;
				message = www.error;
			}
			www.Dispose();
		}
		
		if (error) {
			if (Fail != null)
				Fail(imagePath, message, data);
		} else if (Success != null) {
			Success(imagePath, textures[imagePath], data);
		}
		
		locks[imagePath] = false;
	}
	
	public void LoadResource(string imagePath, object data) {
		bool success = false;
		if (textures.ContainsKey(imagePath)) {
			success = true;
		} else {
			Texture2D texture = Resources.Load<Texture2D>(imagePath);
			if (texture != null) {
				textures[imagePath] = texture;
				success = true;
			}
		}
		if (success)
			Success(imagePath, textures[imagePath], data);
		else
			Fail(imagePath, string.Format("{0} is not an embedded image.", imagePath), data);
	}
	
	public void Unload(string imagePath) {
		imagePath = AssetName(imagePath);
		if (textures.ContainsKey(imagePath)) {
			Destroy(textures[imagePath]);
			textures.Remove(imagePath);
		}
	}
	
	// Unique name for each file.
	string AssetName(string name) {
		name = LoaderTools.ForwardSlash(name).Trim();
		#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
		name = name.ToLower();
		#endif
		return name;
	}
}