/* Player and GUI control.
 * 2014-10-24. Leonardo Molina.
 * 2019-08-05. Last modification.
 */
 
/*	
	Considerations for development:
		Fresh start - IDE and object settings:
			Player Settings
				Api Compatibility Level: .NET 2.0
			InputManager
				Horizontal: a,d
				Vertical: down, up, s, w
				Turn: left, right
			Input:
				Mouse X: sensitivity = 1
				Mouse Y: sensitivity = 1
			Add tags: object, maze, airPuff, reward
			Add layers: Experimenter/Aerial
			Left/Front/Right/Floor camera
				Culling Mask: All except Experimenter
			Off camera:
				Culling Mask: Only UI
			Script order: SetGlobal, Control, Network, Exception Handler, Contacts, Block, Pickup, Client, Monitor, Keyboard.
			Event System Object: Force Module Active.
		Edit.. Project Settings..
			Physics: Layer collision matrix is all to all by default. I changed it to:
				Identity for everyone.
				Default to Experimenter, Floor.
				Floor to Experimenter.
*/

using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Pointers;

#if UNITY_EDITOR
using UnityEditor;
#endif

using Starry = Interphaser.Loader.Starry;
using Pointer = Pointers.Pointer;

public class Control : MonoBehaviour {
	string appName = "SmoothWalk";
	string version = "20190904";
	string settingsId = "21";
	
	// GUI - Information.
	public GameObject mainMenu;
	public GameObject clientMenu;
	public GameObject monitorMenu;
	public Image blackScreen;
	public Image whiteScreen;
	public Transform settingsPanel;
	public Text messageText;
	public Text timeText;
	public Text statusText;
	public GameObject[] mazeList;
	public Camera[] cameraList;
	
	// Save/Load/Network/GUI fields.
	public static string sessionId = "";
	public static string logsFolder = "";
	public static string resourcesFolder = "";
	static Dictionary<string, string> strings = new Dictionary<string, string>();
	
	// Main objects.
	TextureLoader textureLoader;
	BundleLoader bundleLoader;
	Dictionary<string, GameObject> mazes = new Dictionary<string, GameObject>();
	Dictionary<string, List<GameObject>> objects = new Dictionary<string, List<GameObject>>();
	Dictionary<string, string> objectCommands = new Dictionary<string, string>();
	
	GameObject arena;
	Dictionary<string, Field> fields = new Dictionary<string, Field>();
	
	Dictionary<string, Configuration> configuration = new Dictionary<string, Configuration>();
	Dictionary<string, Player> players = new Dictionary<string, Player>();
	List<string> saveParameters = new List<string>();
	List<string> executeParameters = new List<string>();
	List<string> receiveParameters = new List<string>();
	List<string> sendParameters = new List<string>();
	List<string> resendParameters = new List<string>();
	List<string> resetParameters = new List<string>();
	
	Tap tap;
	Gestures gestures;
	PointerDebounce pointerDebounce;
	PointerGetter pointerGetter;
	StreamWriter logWriter;
	
	// Synchronization and i/o.
	Bridge bridge;
	long P02 = 0;
	long P03 = 0;
	long roll;
	object inputLock = new object();
	
	static float timeOffset = 0f;
	static System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
	bool collisions = false;
	float trialLastDuration = 0f;
	
	// Motion variables.
	float[] lSpeedLimits = new float[4]{0f,0f,0f,0f};
	bool touchActive = false;
	Brake lBrake = new Brake(0f);
	Vector3 spawnPosition = Vector3.zero;
	Vector3 spawnRotation = Vector3.zero;
	
	Vector3 lSpeedNetwork = Vector3.zero;
	Vector3 rSpeedNetwork = Vector3.zero;
	Brake lBrakeNetwork = new Brake(0.1f);
	Brake rBrakeNetwork = new Brake(0.1f);
	
	// Game state and helper variables.
	IEnumerator trialRoutine;
	IEnumerator triggerOutRoutine;
	bool loggerOn = false;
	string aboutMessage = "leonardomt@gmail.com";
	string helpMessage = "";
	
	bool busy = true;
	static Dictionary<string,int> status = new Dictionary<string,int>();
	
	// GUI state.
	static string clearMessageId = "";
	
	// Cameras.
	Transform relativeCameras;
	Dictionary<string,Camera> cameras = new Dictionary<string,Camera>();
	Camera currentCamera;
	DeviceOrientation deviceOrientation = DeviceOrientation.Unknown;
	ScreenOrientation screenOrientation = ScreenOrientation.Portrait;
	
	public Camera CurrentCamera {
		get {
			return currentCamera;
		}
	}
	
	void Awake() {
		tap = Components.Get("Tap") as Tap;
		tap.TapChanged += OnTap;
		pointerGetter = Components.Get("PointerGetter") as PointerGetter;
		
		
		pointerDebounce = Components.Get("PointerDebounce") as PointerDebounce;
		//pointerDebounce = Components.gObject.AddComponent<PointerDebounce>();
		gestures = Components.Get("Gestures") as Gestures;
		
		Application.runInBackground = true;
		Screen.sleepTimeout = SleepTimeout.NeverSleep;
	}
	
	void OnConnectionChanged(Bridge bridge, bool connected) {
		// Check serial communication.
		if (connected) {
			// Flash pin.
			bridge.GetBinary(39, 0, 0, 1);
			// Trial pin.
			bridge.GetBinary(38, 0, 0, 1);
			// Input counts.
			bridge.GetBinary(21, 0, 0, 1);
			bridge.GetBinary(20, 0, 0, 1);
			bridge.GetBinary(19, 0, 0, 1);
			bridge.GetBinary(18, 0, 0, 1);
			// Wheel pitch.
			bridge.GetRotation(2, 4, 1);
			// Wheel roll.
			bridge.GetRotation(3, 5, 1);
			AppendMessage("<b>Serial port</b>\nConnected.");
		} else {
			AppendMessage("<b>Serial port</b>\nDisconnected.");
		}
	}
	
	void OnInputChanged(Bridge bridge, int pin, int state) {
		switch (pin) {
			case 38:
				if (state == 1)
					MainThread.Call(CallbackForwarder, new CallbackData("trial", fields["trialDuration"].String, "change"));
				else
					MainThread.Call(CallbackForwarder, new CallbackData("trial", "-1", "change"));
				break;
			case 39:
				MainThread.Call(CallbackForwarder, new CallbackData("triggerOut", fields["triggerOutDuration"].String, "change"));
				break;
			case 18:
			case 19:
			case 20:
			case 21:
				string entry = Elapsed.ToString("#0.0000") + ",count-" + pin + "," + bridge.GetCount(pin, state) + "," + state;
				if (Global.Network.RoleMonitor)
					Global.Network.Send(Network.Recipients.Player, "log", entry + ";");
				Log(entry);
				break;
		}
		
		// Format counts for display.
		ulong P21 = bridge.GetCount(21, 1);
		ulong P20 = bridge.GetCount(20, 1);
		ulong P19 = bridge.GetCount(19, 1);
		ulong P18 = bridge.GetCount(18, 1);
		strings["counts"] = "";
		if (P21 > 0) strings["counts"] += " P21:" + P21;
		if (P20 > 0) strings["counts"] += " P20:" + P20;
		if (P19 > 0) strings["counts"] += " P19:" + P19;
		if (P18 > 0) strings["counts"] += " P18:" + P18;
		strings["counts"] = strings["counts"].Trim();
	}
	
	class CallbackData {
		public string Parameter {set; get;}
		public string Value {set; get;}
		public string Option {set; get;}
		
		public CallbackData(string parameter, string value, string option) {
			Parameter = parameter;
			Value = value;
			Option = option;
		}
	}
	
	void CallbackForwarder(object data) {
		CallbackData cdata = (CallbackData) data;
		Callback(cdata.Parameter, cdata.Value, cdata.Option);
	}
	
	IEnumerator Start() {
		// Helper.
		strings["counts"] = "";
		strings["lSpeed"] = "";
		strings["rSpeed"] = "";
		strings["position"] = "";
		strings["rotation"] = "";
		status["menu"] = 0;
		status["trial"] = 0;
		status["triggerOut"] = 0;
		status["blockActive"] = 0;
		status["pixelWidth"] = 0;
		status["pixelHeight"] = 0;
		status["screenWidth"] = 0;
		status["screenHeight"] = 0;
		
		bridge = new Bridge(115200);
		bridge.ConnectionChanged += OnConnectionChanged;
		bridge.InputChanged += OnInputChanged;
		
		bundleLoader = new BundleLoader();
		bundleLoader.Success += OnBundleLoadSuccess;
		bundleLoader.Fail += OnBundleLoadFail;
		
		textureLoader = new TextureLoader();
		textureLoader.Success += OnTextureLoadSuccess;
		textureLoader.Fail += OnTextureLoadFail;
		
		deviceOrientation = Input.deviceOrientation;
		screenOrientation = Screen.orientation;		
		
		// If settings changed substantially, clear preferences when running for the first time.
		if (!PlayerPrefs.GetString("settingsId", "").Equals(settingsId)) {
			Debug.Log("Cleared settings during startup.");
			PlayerPrefs.DeleteAll();
			PlayerPrefs.SetString("settingsId", settingsId);
		}
		
		stopwatch.Reset();
		stopwatch.Start();
		triggerOutRoutine = TriggerOutRoutine(0f, 0);
		
		// Help and about.
		helpMessage = "<b>Help</b>\nSmoothWalk translates walking gestures into movement in a virtual environment.\nTo hide and restore this menu, double tap on any corner, press the <i>Back button</i>, or press the <i>Escape key</i>, whichever is available.\n\n<b>Extended View</b>\nMultiple devices can be synced and arranged around the player to display a greater field of view and create more immersive environments:\n-Using the dropdown list under <i>Device Settings</i>, set the mode of one of the devices to <i>Control</i>, then click on <i>Configure</i> and list the ID# of each monitor device.\n-Set the mode of all other devices to <i>Monitor</i>\n\n<b>Settings</b>\n-Choose a view on each monitor that corresponds to its relative arrangement to the player.\n-<i>Auto-align</i> makes the avatar turn automatically near walls.\n-<i>X</i> and <i>Y gains</i> change the sensitivity of translational movements\n\n<b>Notes</b>\n-Pickup zones are always visible in <i>Aerial View</i>. The player is required to wait for a fixed time <i>(delay)</i> before they trigger.\n-Mecanical trigger can be accomplished by connecting a valve to the <i>external board</i>. Connect this <i>board</i> to the control device preferably or else to any of the monitor devices. A positive pulse will be put for a given duration to the corresponding hardware pin.\n-Data acquisition systems listening to <i>trigger input</i>, can also be synchronized with this device by clicking <i>Trigger out</i>. The screen will flash for a given duration and a pulse will be put on the corresponding hardware pin.\n-Log data associated with each session (timed inputs, motion data and rewards) are saved at regular intervals. Click <i>Log file</i> to display the location of this file.\n-Further configurations (e.g. maze dimensions, location and number of pickup zones, etc) are possible by entering commands or via the API. Please refer to the extended documentation for details.";
		aboutMessage = string.Format("<b>About:</b>\nSmoothWalk - version {0}\nPlease cite if you use for any purposes.\nContact: leonardomt@gmail.com\n\n", version);
		
		// Output files and resources.
		string nowString = DateTime.Now.ToString("yyyyMMddHHmmss");
		sessionId = string.Format("VR{0}", nowString);
		logsFolder = Application.persistentDataPath;
		resourcesFolder = Path.Combine(logsFolder, "Resources");
		
		if (Tools.IsWindows) {
			try {
				string tmp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), appName);
				Directory.CreateDirectory(tmp);
				logsFolder = tmp;
			} catch {}
			try {
				string tmp = Path.Combine(logsFolder, "Resources");
				Directory.CreateDirectory(tmp);
				resourcesFolder = tmp;
			} catch {}
		} else if (Tools.IsAndroid) {
			AndroidJavaClass toolsJC = new AndroidJavaClass("com.interphaser.tools.Tools");
			string sdcard = toolsJC.CallStatic<string>("getExternalStorageDirectory");
			//string sdcard = Hardware.GetAndroidExternalFilesDir();
			logsFolder = Path.Combine(sdcard, "SmoothWalk");
			resourcesFolder = Path.Combine(logsFolder, "Resources");
			if (!Directory.Exists(logsFolder))
				Directory.CreateDirectory(logsFolder);
			if (!Directory.Exists(resourcesFolder))
				Directory.CreateDirectory(resourcesFolder);
		}
		
		// Find cameras.
		cameras["Aerial"] = GameObject.Find("AerialCamera").GetComponent<Camera>();
		cameras["Off"] = GameObject.Find("OffCamera").GetComponent<Camera>();
		foreach (Camera cam in cameraList)
			cameras.Add(cam.name, cam);
			
		// Find player and relative cameras.
		players["default"] = Global.Player;
		relativeCameras = GameObject.Find("RelativeCameras").GetComponent<Transform>();
		
		// Setup fields.
		Configure();
		logWriter = new StreamWriter(configuration["filename"].value, true, Encoding.UTF8, 4096);
		BuildMenu();
		
		// Start logging.
		Log(ElapsedString + ",date," + nowString);
		Log(ElapsedString + ",sessionId," + sessionId);
		Log(ElapsedString + ",version," + version);
		Log(ElapsedString + ",level," + SceneManager.GetActiveScene().name);
		
		View("Off");
		UpdateView(true);
		
		
		// Find mazes.
		foreach (GameObject mazeObject in mazeList)
			mazes.Add(mazeObject.name.ToLower(), mazeObject);
		
		// Force to show then hide all menu canvases and gameobjects so that its children InputFields behave and draw correctly.
		blackScreen.enabled = true;
		Global.Menu.Show(mainMenu, clientMenu, monitorMenu);
		Tools.Visible(true, mainMenu, clientMenu, monitorMenu);
		yield return new WaitForEndOfFrame();
		Tools.Visible(false, mainMenu, clientMenu, monitorMenu);
		yield return new WaitForEndOfFrame();
		Tools.Visible(true, mainMenu, clientMenu, monitorMenu);
		yield return new WaitForEndOfFrame();
		Global.Menu.Hide(mainMenu, clientMenu, monitorMenu);
		Global.Menu.Show(mainMenu);
		blackScreen.enabled = false;
		
		// First use default settings then load from disk.
		Defaults("quiet");
		LoadSettings();
		
		// Spawn player. Clamp if necessary. Forward data.
		Global.Player.Position = spawnPosition;
		Global.Player.Euler = spawnRotation;
		ForwardPosition("default");
		ForwardRotation("default");
		
		// Show about.
		AppendMessage(helpMessage);
		
		// Start other routines.
		StartCoroutine(Routine5());
		StartCoroutine(Routine1());
		StartCoroutine(Routine005());
		
		busy = false;
		collisions = true;
	}
	
	void Log(string text) {
		logWriter.Write(text + "\n");
	}
	
	bool LoadMaze(string name) {
		bool success = false;
		name = name.ToLower();
		if (mazes.ContainsKey(name)) {
			success = true;
			if (!name.Equals(fields["maze"])) {
				foreach (string mazeName in mazes.Keys)
					mazes[mazeName].SetActive(mazeName.Equals(name));
			}
		}
		return success;
	}
	
	void Trial(int stage) {
		Trial(trialLastDuration, stage);
	}
	
	void Trial(float duration, int stage) {
		if (trialRoutine != null)
			StopCoroutine(trialRoutine);
		trialRoutine = TrialRoutine(duration, stage);
		StartCoroutine(trialRoutine);
	}
	
	
	IEnumerator TrialRoutine(float duration, int stage) {
		trialLastDuration = duration;
		status["trial"] = stage;
		
		// 0: Pin=Low / Log=Low / Clear screen.
		if (status["trial"] == 0) {
			blackScreen.enabled = false;
			whiteScreen.enabled = false;
			// Switch pin 40 to low.
			bridge.SetBinary(40, 0);
		}
		
		// Pin=Low / Log=Low / Black screen.
		if (status["trial"] == 1) {
			// Stop avatar.
			lBrake.Reset();
			lBrakeNetwork.Reset();
			rBrakeNetwork.Reset();
			Global.Player.LinearSpeed = new Vector3(0f, 0f, 0f);
			Global.Player.AngularSpeed = new Vector3(0f, 0f, 0f);

			// Switch pin 40 to low.
			bridge.SetBinary(40, 0);
			if (duration > 0f)
				blackScreen.enabled = true;
			
			if (Global.Network.RolePlayer) {
				Forward("trial", "low");
				Log(ElapsedString + ",trial,low");
			}
			
			// Disable player collisions until new trial applies.
			collisions = false;
			// If available (control or monitor), select and apply new trial.
			RandomTrial();
			// New parameters will apply in other devices during wait time.
			yield return new WaitForSeconds(duration);
			status["trial"] = 2;
		}
		
		// Log=High / Pin=High.
		if (status["trial"] == 2) {
			// Complete trial reset.
			if (Global.Network.RolePlayer) {
				// Respawn.
				Global.Player.transform.position = spawnPosition;
				Global.Player.transform.rotation = Quaternion.Euler(spawnRotation);
				ForwardPosition("default");
				ForwardRotation("default");
				// Reset pickup interval.
				foreach (GameObject gObj in GameObject.FindGameObjectsWithTag("object")) {
					Pickup[] pickups = gObj.GetComponentsInChildren<Pickup>();
					foreach (Pickup pickup in pickups)
						pickup.Rearm();
				}
				Forward("trial", "high");
				Log(ElapsedString + ",trial,high");
			}
			
			// Switch pin 40 to high.
			bridge.SetBinary(40, 1);
			
			blackScreen.enabled = false;
			whiteScreen.enabled = false;
			yield return new WaitForEndOfFrame();
			
			// Resume behavior.
			status["trial"] = 0;
			// Resume collisions.
			collisions = true;
		}
	}
	
	void TriggerOut(int stage) {
		TriggerOut(triggerOutLastDuration, stage);
	}
	
	void TriggerOut(float duration, int stage) {
		StopCoroutine(triggerOutRoutine);
		triggerOutRoutine = TriggerOutRoutine(duration, stage);
		StartCoroutine(triggerOutRoutine);
	}
	
	float triggerOutLastDuration = 0f;
	IEnumerator TriggerOutRoutine(float duration, int stage) {
		triggerOutLastDuration = duration;
		status["triggerOut"] = stage;
		
		// 0: Pin=Low / Log=Low / Clear screen.
		if (status["triggerOut"] == 0) {
			blackScreen.enabled = false;
			whiteScreen.enabled = false;
			// Switch pin 41 to low.
			bridge.SetBinary(41, 0);
		}
		
		// Pin=Low / Log=Low / Black screen.
		if (status["triggerOut"] == 1) {
			status["menu"] = Global.Menu.Visible ? 1 : 0;
			if (duration > 0f)
				Global.Menu.Visible = false;
			
			// Switch pin 41 to low.
			bridge.SetBinary(41, 0);
			if (duration > 0f)
				blackScreen.enabled = true;
			
			// Clear input counts.
			if (Global.Network.RolePlayer) {
				Log(ElapsedString + ",triggerOut,low");
			}
			yield return new WaitForSeconds(0.5f*duration);
			status["triggerOut"] = 2;
		}
		
		// Log=High / Pin=High / White screen.
		if (status["triggerOut"] == 2) {
			if (Global.Network.RolePlayer) {
				Log(ElapsedString + ",triggerOut,high");
			}
			
			// Switch pin 41 to high.
			bridge.SetBinary(41, 1);
			
			blackScreen.enabled = false;
			whiteScreen.enabled = true;
			yield return new WaitForSeconds(0.5f*duration);
			status["triggerOut"] = 3;
		}
		
		if (status["triggerOut"] == 3) {
			// Clear screen.
			blackScreen.enabled = false;
			whiteScreen.enabled = false;

			yield return new WaitForEndOfFrame();
			Global.Menu.Visible = status["menu"] == 1;
			
			// Resume behavior.
			status["triggerOut"] = 0;
		}
	}
	
	void RandomTrial() {
		if (trials.Count > 1) {
			float[] probabilities = trials.Select(item => item.Item1).ToArray();
			int luck = Tools.Dice(probabilities);
			string trial = trials.ElementAt(luck).Item2;
			Execute(trial);
		}
	}
	
	void ReceiveRoutine() {
		while (true) {
			string instructions = Global.Network.Data;
			string instruction = "";
			string source = "";
			if (Tools.Parse(ref instructions, ref source)) {
				source = source.Trim();
				if (source.Equals("log")) {
					if (Global.Network.RolePlayer)
						while (Tools.Collect(ref instructions, ref instruction)) {
							Log(instruction);
						}
				} else {
					while (Tools.Collect(ref instructions, ref instruction)) {
						string parsing = instruction;
						string parameter = Tools.Parse(ref parsing).Trim();
						string values = parsing;
						bool success = receiveParameters.Contains(parameter) && Callback(parameter, values, source);
						if (!success) {
							string message = string.Format("Failed instruction with source={0}: \"{1}\"", source, instruction);
							Debug.Log(message);
							// AppendMessage(message);
						}
					}
				}
			} else {
				break;
			}
		}
	}
	
	IEnumerator Routine5() {
		while (true) {
			logWriter.Flush();
			// Append file with changes.
			PlayerPrefs.Save();
			// if (loggerOn) {
				// bool result = Global.Logger.Append(OnPostAppend, fields["filename"].String);
				// Debug.Log("Requested, result:" + result);
			// }
			yield return new WaitForSeconds(5f);
		}
	}
	
	IEnumerator Routine1() {
		while (true) {
			UpdateView(false);
			
			// Recently added devices receive up to date settings, and position and rotation from everyone.
			if (Global.Network.RolePlayer) {
				StringBuilder settingsBuilder = new StringBuilder();
				settingsBuilder.Append("elapsed," + ElapsedString + ";");
				settingsBuilder.Append("sessionId," + sessionId + ";");
				foreach (string parameter in resendParameters)
					settingsBuilder.Append(parameter + "," + fields[parameter].String + ";");
				settingsBuilder.Append(AllObjects);
				string settings = settingsBuilder.ToString();
				Global.Network.Send(Network.Recipients.Monitors, "default", settings);
				
				foreach (string playerId in players.Keys) {
					ForwardPosition(playerId);
					ForwardRotation(playerId);
				}
			}
			
			// Keep network from sleeping.
			Tools.Headers("http://www.example.com", OnHeaders);
			yield return new WaitForSeconds(1f);
		}
	}
	
	string AllObjects {
		get {
			StringBuilder settings = new StringBuilder();
			foreach (string objectCommand in objectCommands.Values)
				settings.Append("objects," + objectCommand + ";");
			if (objectCommands.Count == 0)
				settings.Append("objects;");
			return settings.ToString();
		}
	}
	
	IEnumerator Routine005() {
		// Display time and counts.
		while (true) {
			if (Global.Menu.Visible) {
				timeText.text = string.Format("Session ID#: {0}", fields["sessionId"].String);
				if (Global.Network.RolePlayer || Global.Network.Enable)
					timeText.text += string.Format("\nElapsed time: {0:0000.00}s", Elapsed);
				// Status.
				string text = "Device ID#: " + ID + "\n" + Status;
				if (strings["counts"].Length > 0) {
					loggerOn = true;
					text += "\n" + strings["counts"];
				}
				statusText.text = text;
			}
			yield return new WaitForSeconds(0.05f);
		}
	}
	
	public void ToggleMenu() {
		Global.Menu.Toggle();
	}
	
	void OnTap(int count, Vector2 position) {
		if (count > 1) {
			float tapX = position.x;
			float tapY = position.y;
			// Only at the corners, defined by 2cm from the edges.
			float h = 2f/2.54f*Screen.dpi;
			float v = 2f/2.54f*Screen.dpi;
			float left = h;
			float right = Screen.width - h;
			float bottom = v;
			float top = Screen.height - v;
			if ((tapX > right || tapX < left) && (tapY > top || tapY <bottom))
				tapHappened = true;
		}
	}
	
	bool tapHappened = false;
	
	void Update() {
		if (busy)
			return;
		
		// Always update these so that wheel motion is not accumulated during inter trials.
		long p02Diff = bridge.GetValue(2) - P02;
		P02 = bridge.GetValue(2);
		long p03Diff = bridge.GetValue(3) - P03;
		P03 = bridge.GetValue(3);
		
		ReceiveRoutine();
		
		// Capture back button, escape and double tap.
		bool userAction = false;
		// Escape or double tap.
		if (Input.GetKeyDown(KeyCode.Escape)) {
			userAction = true;
		} else if (tapHappened) {
			userAction = true;
			tapHappened = false;
		}
		
		// Stop synchronization or toggle menu on user action.
		if (userAction) {
			if (status["trial"] != 1 && (status["triggerOut"] == 0 || status["triggerOut"] == 3)) {
				Global.Menu.Toggle();
			}
			if (status["trial"] > 0)
				Callback("trial", "-1", "change");
			if (status["triggerOut"] > 0)
				Callback("triggerOut", "-1", "change");
		}
		
		if (status["trial"] != 0 || (status["triggerOut"] != 0 && status["triggerOut"] != 3))
			return;
		
		
		// Initialize speed.
		Vector3 linearSpeed = Vector3.zero;
		Vector3 angularSpeed = Vector3.zero;
		
		// Movement from wheel.
		lock (inputLock) {
			linearSpeed.z += fields["yGain"].Number * p02Diff / Time.deltaTime;
			linearSpeed.x += fields["xGain"].Number * p03Diff / Time.deltaTime;
			angularSpeed.y += fields["rotationGain"].Number * p03Diff / Time.deltaTime;
		}
		
		// Movement from touch.
		touchActive = !Global.Menu.Visible && (pointerGetter.DragCount > 0);
		bool forwardTouch = Global.Network.RoleMonitor && Global.Monitor.ForwardInputs;
		if (touchActive && (Global.Network.RolePlayer || forwardTouch)) {
			if (pointerGetter.DragCount > 0) {
				var builder = new Dictionary<int, string>();
				foreach (Pointer pointer in pointerDebounce.Pointers) {
				//foreach (Pointer pointer in pointerGetter.DragPointers) {
					builder[pointer.fingerId] = string.Format("{0:0.0000},{1:0.0000}", pointer.position.x, pointer.position.y);
				}
				string touchString = "";
				foreach (string positionString in builder.Values)
					touchString += "," + positionString;
				string entry = ElapsedString + ",touch" + touchString;
				if (Global.Network.RolePlayer) {
					Log(entry);
				} else if (forwardTouch) {
					Global.Network.Send(Network.Recipients.Player, "log", entry + ";");
				}
			}
			
			// Touch inputs.
			// Avatar moves forward with Contacts' y-axis and laterally with Contacts' x-axis.
			Vector2 touchLinearSpeed = -Global.UPP * gestures.DragLinearSpeed;

			// All speeds are relative to avatar's axis.
			if (!VirtualRotation)
				touchLinearSpeed = Tools.RotateDegrees(touchLinearSpeed, Global.Player.Euler.y);
			touchLinearSpeed.x *= fields["xGain"].Number;
			touchLinearSpeed.y *= fields["yGain"].Number;
			
			// Movement from deviation.
			linearSpeed.x += touchLinearSpeed.x;
			linearSpeed.z += touchLinearSpeed.y;
		}
		
		// Speed from touch and serial slowly damps if requested.
		lBrake.Update(ref linearSpeed, Time.deltaTime);
		
		// Apply speed limits before keyboard or network.
		linearSpeed.x = Mathf.Clamp(linearSpeed.x, lSpeedLimits[0], lSpeedLimits[1]);
		linearSpeed.z = Mathf.Clamp(linearSpeed.z, lSpeedLimits[2], lSpeedLimits[3]);
		
		// Movement from keyboard.
		Vector3 lSpeedKeyboard = 10f*(new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical")));
		float rSpeedKeyboard = 120f * Input.GetAxis("Turn");
		if (!Global.Menu.Visible) {
			linearSpeed.x += Mathf.Max(Mathf.Abs(fields["xGain"].Number), 1f) * lSpeedKeyboard.x;
			linearSpeed.z += Mathf.Max(Mathf.Abs(fields["yGain"].Number), 1f) * lSpeedKeyboard.z;
			angularSpeed.y += Mathf.Max(Mathf.Abs(fields["rotationGain"].Number), 1f) * rSpeedKeyboard;
		}
		
		if (Global.Network.RoleMonitor) {
			// Forward linear and angular speeds to control.
			string dlx = linearSpeed.x.ToString("#0.0000");
			string dlz = linearSpeed.z.ToString("#0.0000");
			string dly = linearSpeed.y.ToString("#0.0000");
			string drx = "";
			string drz = "";
			string dry = (-angularSpeed.y).ToString("#0.0000");
			string lSpeed = "linearSpeed," + dlx + "," + dlz + "," + dly + ";";
			string rSpeed = "angularSpeed," + drx + "," + drz + "," + dry + ";";
			if (!strings["lSpeed"].Equals(lSpeed)) {
				strings["lSpeed"] = lSpeed;
				Global.Network.Send(Network.Recipients.Player, "default", lSpeed);
			}
			if (!strings["rSpeed"].Equals(rSpeed)) {
				strings["rSpeed"] = rSpeed;
				Global.Network.Send(Network.Recipients.Player, "default", rSpeed);
			}
		} else {
			// Movement from monitors.
			lBrakeNetwork.Update(ref lSpeedNetwork, Time.deltaTime);
			linearSpeed += lSpeedNetwork;
			rBrakeNetwork.Update(ref rSpeedNetwork, Time.deltaTime);
			angularSpeed += rSpeedNetwork;
			
			// Cancel autoAlign process if network or keyboard are turning.
			if (rSpeedNetwork.sqrMagnitude > 0f || Mathf.Abs(rSpeedKeyboard) > 0f)
				Global.AutoAlign.Cancel();
			
			if (VirtualRotation) {
				// Movement from maze: Virtual rotation.
				if (rSpeedNetwork.sqrMagnitude < 0.001f && fields["autoAlign"].Equals("1"))
					angularSpeed.y += Global.AutoAlign.Speed;
				Global.Player.AngularSpeed = angularSpeed;
			}
			
			// Make linear speed relative to player's forward direction.
			Global.Player.LinearSpeed = Global.Player.Rotation * linearSpeed;
			
			// Forward own's movement as soon as it happens.
			Player player = Global.Player;
			string lx = player.Position.x.ToString("#0.0000");
			string lz = player.Position.z.ToString("#0.0000");
			string ly = player.Position.y.ToString("#0.0000");
			string rx = player.Euler.x.ToString("#0.0000");
			string rz = player.Euler.z.ToString("#0.0000");
			string ry = Tools.Mirror(player.Euler.y).ToString("#0.0000");
			string position = lx + "," + lz + "," + ly;
			string rotation = rx + "," + rz + "," + ry;
			string elapsedString = ElapsedString;
			if (!strings["position"].Equals(position)) {
				strings["position"] = position;
				PlayerForward("position" + "," + position, "default");
				Log(elapsedString + "," + "position" + "," + position);
			}
			if (!strings["rotation"].Equals(rotation)) {
				strings["rotation"] = rotation;
				PlayerForward("rotation" + "," + rotation, "default");
				Log(elapsedString + "," + "rotation" + "," + rotation);
			}
			
			// Enable data log if player moves.
			if (!loggerOn && (linearSpeed.sqrMagnitude > 0 || angularSpeed.sqrMagnitude > 0))
				loggerOn = true;
		}
		
		// Update position/rotation of cameras.
		relativeCameras.position = Global.Player.Position;
		if (VirtualRotation)
			relativeCameras.rotation = Global.Player.Rotation;
		if (currentCamera == cameras["Aerial"])
			cameras["Aerial"].transform.position = Vector3.MoveTowards(cameras["Aerial"].transform.position, new Vector3(Global.Player.Position.x, cameras["Aerial"].transform.position.y, Global.Player.Position.z), 500f*Time.deltaTime);
	}
	
	void UpdateView(bool force) {
		if (force || deviceOrientation != Input.deviceOrientation || screenOrientation != Screen.orientation || status["screenWidth"] != Screen.width || status["screenHeight"] != Screen.height || status["pixelWidth"] != currentCamera.pixelWidth || status["pixelHeight"] != currentCamera.pixelHeight) {
			deviceOrientation = Input.deviceOrientation;
			status["pixelWidth"] = currentCamera.pixelWidth;
			status["pixelHeight"] = currentCamera.pixelHeight;
			status["screenWidth"] = Screen.width;
			status["screenHeight"] = Screen.height;
			
			foreach (Camera camera in cameras.Values)
				camera.fieldOfView = 2f*Mathf.Atan(0.5f*Global.UPP*Screen.height/fields["monitorDistance"].Number)*Mathf.Rad2Deg;
			//cameras["Floor"].orthographicSize = 0.5f*currentCamera.pixelHeight*Global.UPP;
		}
	}
	
	bool DeviceMode(string deviceMode) {
		bool success = true;
		// Recover own's sessionId.
		fields["sessionId"].String = sessionId;
		switch (deviceMode) {
			case "Control":
				Global.Network.Role = Network.Roles.Client;
				break;
			case "Monitor":
				Global.Network.Role = Network.Roles.Monitor;
				break;
			default:
				success = false;
				break;
		}
		return success;
	}
	
	void DisableCameras() {
		// Reset.
		// Camera spans the whole view and is disabled.
		foreach (Camera cam in cameras.Values) {
			cam.rect = new Rect(0f, 0f, 1f, 1f);
			cam.enabled = false;
		}
	}
	
	bool View(string view) {
		bool success = true;
		
		fields["azimuthView"].Enabled = false;
		fields["altitudeView"].Enabled = false;
		DisableCameras();
		// Switch view.
		switch (view) {
			case "West":
				cameras[view].enabled = true;
				currentCamera = cameras[view];
				break;
			case "North":
				cameras[view].enabled = true;
				currentCamera = cameras[view];
				break;
			case "East":
				cameras[view].enabled = true;
				currentCamera = cameras[view];
				break;
			case "South":
				cameras[view].enabled = true;
				currentCamera = cameras[view];
				break;
			case "Floor":
				cameras[view].enabled = true;
				currentCamera = cameras[view];
				break;
			case "Manual":
				fields["azimuthView"].Enabled = true;
				fields["altitudeView"].Enabled = true;
				cameras[view].enabled = true;
				currentCamera = cameras[view];
				break;
			case "Aerial":
				cameras[view].enabled = true;
				currentCamera = cameras[view];
				break;
			case "Off":
				cameras[view].enabled = true;
				currentCamera = cameras[view];
				break;
			case "Split":
				cameras["West"].enabled = true;
				cameras["North"].enabled = true;
				cameras["East"].enabled = true;
				cameras["West"].rect = new Rect(0.00f, 0.00f, 0.33f, 1.00f);
				cameras["North"].rect = new Rect(0.33f, 0.00f, 0.34f, 1.00f);
				cameras["East"].rect = new Rect(0.67f, 0.00f, 0.33f, 1.00f);
				currentCamera = cameras["North"];
				break;
			default:
				success = false;
				break;
		}
		return success;
	}
	
	void Defaults(string source) {
		Callback("objects", "", source); // Manually clear objects.
		foreach (string parameter in resetParameters)
			Callback(parameter, configuration[parameter].value, source);
		if (source != "quiet")
			ShowMessage("<b>Settings</b>\nSettings have been reset to factory defaults.");
	}
	
	void LoadSettings() {
		string value = "";
		foreach (string parameter in saveParameters) {
			value = PlayerPrefs.GetString(parameter, configuration[parameter].value);
			Callback(parameter, value, "change");
		}
	}
	
	public class Configuration {
		public bool save = false;
		public bool log = false;
		public bool execute = false;
		public bool receive = false;
		public bool send = false;
		public bool resend = false;
		public bool reset = false;
		public bool trigger = false;
		public string value = "";
		
		public Configuration(bool save, bool log, bool execute, bool receive, bool send, bool resend, bool reset, bool trigger, string value) {
			this.value = value;
			Configure(save, log, execute, receive, send, resend, reset, trigger);
		}
		
		public Configuration(int save, int log, int execute, int receive, int send, int resend, int reset, int trigger, string value) {
			this.value = value;
			Configure(save == 1, log == 1, execute == 1, receive == 1, send == 1, resend == 1, reset == 1, trigger == 1);
		}
		
		void Configure(bool save, bool log, bool execute, bool receive, bool send, bool resend, bool reset, bool trigger) {
			this.save = save;
			this.log = log;
			this.execute = execute;
			this.receive = receive;
			this.send = send;
			this.resend = resend;
			this.reset = reset;
			this.trigger = trigger;
		}
	}
	
	public void Configure() {
		string spawnPosition = "0,0,1";
		string spawnRotation = "0,0,90";
		string filename = Path.Combine(logsFolder, sessionId + ".csv");
		
		// Execution order.
		configuration[             "filename"] = new Configuration(0, 1, 1, 1, 0, 0, 1, 0, filename);
		
		configuration[                 "maze"] = new Configuration(1, 1, 1, 1, 1, 1, 1, 0, "plain");
		configuration[      "virtualRotation"] = new Configuration(1, 0, 1, 1, 1, 1, 1, 0, "1");
		configuration[            "autoAlign"] = new Configuration(1, 1, 1, 1, 1, 1, 1, 0, "1");
		configuration[          "azimuthView"] = new Configuration(1, 1, 1, 1, 0, 0, 1, 0, "0");
		configuration[                 "view"] = new Configuration(1, 1, 1, 1, 0, 0, 1, 0, "North");
		
		// Settings panel.
		configuration[       "deviceSettings"] = new Configuration(1, 0, 0, 0, 0, 0, 1, 0, "");
		configuration[           "deviceMode"] = new Configuration(1, 0, 1, 0, 0, 0, 0, 0, "Control");
		configuration[              "setMode"] = new Configuration(0, 0, 0, 0, 0, 0, 0, 1, "");
		
		configuration[         "viewSettings"] = new Configuration(1, 0, 0, 0, 0, 0, 1, 0, "");
		configuration[      "monitorDistance"] = new Configuration(1, 1, 1, 1, 1, 1, 1, 0, "10");
		
		configuration[       "motionSettings"] = new Configuration(1, 0, 0, 0, 0, 0, 1, 0, "");
		configuration[         "rotationGain"] = new Configuration(1, 1, 1, 1, 1, 1, 1, 0, "0.0000");
		configuration[                "xGain"] = new Configuration(1, 1, 1, 1, 1, 1, 1, 0, "0.0000");
		configuration[                "yGain"] = new Configuration(1, 1, 1, 1, 1, 1, 1, 0, "1.0000");
		configuration[    "linearSpeedLimits"] = new Configuration(1, 1, 1, 1, 1, 1, 1, 0, "-1000,1000,-1000,1000");
		
		configuration[      "triggerSettings"] = new Configuration(1, 0, 0, 0, 0, 0, 1, 0, "");
		
		configuration[        "trialDuration"] = new Configuration(1, 1, 1, 1, 1, 1, 1, 0, "1.0000");
		configuration[             "setTrial"] = new Configuration(0, 0, 0, 0, 0, 0, 0, 1, "1");
		configuration[      "triggerDuration"] = new Configuration(1, 1, 1, 1, 1, 1, 1, 0, "1.0000");
		configuration[           "triggerPin"] = new Configuration(1, 0, 1, 1, 1, 1, 1, 0, "13");
		configuration[           "setTrigger"] = new Configuration(0, 0, 0, 0, 0, 0, 0, 1, "1");
		
		configuration["miscellaneousSettings"] = new Configuration(1, 0, 0, 0, 0, 0, 0, 0, "");
		configuration[           "triggerOut"] = new Configuration(0, 1, 1, 1, 1, 0, 0, 1, "0"); // Logs via routine.
		configuration[   "triggerOutDuration"] = new Configuration(1, 1, 1, 1, 1, 0, 1, 0, "2");
		configuration[        "setTriggerOut"] = new Configuration(0, 0, 1, 0, 0, 0, 0, 1, "0");
		configuration[            "userEntry"] = new Configuration(0, 1, 1, 1, 1, 0, 0, 1, "");  // Escapes/unscapes interally according to role.
		configuration[         "setUserEntry"] = new Configuration(0, 0, 0, 0, 0, 0, 0, 1, "");
		configuration[              "logFile"] = new Configuration(0, 0, 1, 0, 0, 0, 0, 1, "");
		configuration[        "resetSettings"] = new Configuration(0, 0, 1, 1, 0, 0, 0, 1, "1");
		configuration[         "flexKeyboard"] = new Configuration(1, 0, 1, 1, 0, 0, 1, 0, "0");
		configuration[                "about"] = new Configuration(0, 0, 1, 0, 0, 0, 0, 1, "");
		
		// Not parameters.
		configuration[              "elapsed"] = new Configuration(0, 0, 1, 1, 1, 0, 0, 0, "0"); // Resends manually.
		configuration[            "sessionId"] = new Configuration(0, 0, 0, 1, 0, 0, 0, 0, sessionId); // Resends manually.
		configuration[             "position"] = new Configuration(0, 1, 1, 1, 1, 0, 1, 1, spawnPosition);
		configuration[             "rotation"] = new Configuration(0, 1, 1, 1, 1, 0, 0, 1, spawnRotation);
		configuration[          "linearSpeed"] = new Configuration(0, 0, 1, 1, 0, 0, 1, 1, "");
		configuration[         "angularSpeed"] = new Configuration(0, 0, 1, 1, 0, 0, 1, 1, "");
		
		// Hidden.
		configuration[                 "exit"] = new Configuration(0, 0, 0, 0, 0, 0, 0, 1, "");
		configuration[                 "help"] = new Configuration(0, 0, 1, 0, 0, 0, 0, 1, "");
		configuration[              "execute"] = new Configuration(1, 0, 0, 0, 0, 0, 0, 0, "");
		configuration[           "setExecute"] = new Configuration(0, 0, 0, 0, 0, 0, 0, 1, "");
		configuration[              "objects"] = new Configuration(1, 1, 1, 1, 1, 0, 1, 0, "floor, plane, 0, 0, 0, 700, 1, 700, 90, 0, 0, image-embedded, rocks, 175, 175, stop");
		configuration[            "transform"] = new Configuration(0, 1, 1, 1, 1, 0, 0, 1, "");
		configuration[         "altitudeView"] = new Configuration(1, 0, 1, 1, 0, 0, 1, 0, "0");
		configuration[       "cameraPosition"] = new Configuration(1, 0, 1, 1, 0, 0, 1, 0, "0,0,0");
		configuration[        "brakeDuration"] = new Configuration(1, 1, 1, 1, 1, 1, 1, 0, "0.0000");
		configuration[                "trial"] = new Configuration(0, 1, 1, 1, 1, 0, 0, 1, "0");
		configuration[                 "tone"] = new Configuration(0, 1, 1, 1, 1, 0, 0, 1, "");
		configuration[              "trigger"] = new Configuration(0, 1, 1, 1, 1, 0, 0, 1, "");
		configuration[               "pickup"] = new Configuration(0, 1, 1, 1, 1, 0, 0, 1, "");
		configuration[        "spawnPosition"] = new Configuration(1, 1, 1, 1, 1, 1, 1, 0, spawnPosition);
		configuration[        "spawnRotation"] = new Configuration(1, 1, 1, 1, 1, 1, 1, 0, spawnRotation);
		
		
		// save, log, execute, receive, send, resend, reset, isTrigger
		foreach (string parameter in configuration.Keys) {
			if (configuration[parameter].save)
				saveParameters.Add(parameter);
			if (configuration[parameter].execute)
				executeParameters.Add(parameter);
			if (configuration[parameter].receive)
				receiveParameters.Add(parameter);
			if (configuration[parameter].send)
				sendParameters.Add(parameter);
			if (configuration[parameter].resend)
				resendParameters.Add(parameter);
			if (configuration[parameter].reset)
				resetParameters.Add(parameter);
		}
	}
	
	public void BuildMenu() {
		TextField input;
		ButtonField button;
		SliderField slider;
		LabelField label;
		ListField list;
		string parameter;
		
		float y = 0f;
		float h = 40f;
		float t = 50f;
		
		// Device Settings.
		parameter = "deviceSettings";
		label = new LabelField(ToggleCallback, parameter, configuration[parameter].value); fields[parameter] = label;
		label.ScaleXSetY(settingsPanel, 0f, 1f, y, h);
		label.Prefix = "Device Settings";
		
		y -= h;
		parameter = "deviceMode";
		list = new ListField(NormalCallback, parameter); fields[parameter] = list;
		list.ScaleXSetY(settingsPanel, 0f, 0.75f, y, h);
		list.String = "Control";
		list.String = "Monitor";
		list.String = configuration[parameter].value;
		
		parameter = "setMode";
		button = new ButtonField(NormalCallback, parameter, configuration[parameter].value); fields[parameter] = button;
		button.ScaleXSetY(settingsPanel, 0.75f, 1f, y, h);
		button.Prefix = "Configure";
		
		// View Settings.
		y -= t;
		parameter = "viewSettings";
		label = new LabelField(ToggleCallback, parameter, configuration[parameter].value); fields[parameter] = label;
		label.ScaleXSetY(settingsPanel, 0f, 1f, y, h);
		label.Prefix = "View Settings";
		
		y -= h;
		parameter = "view";
		list = new ListField(NormalCallback, parameter); fields[parameter] = list;
		list.ScaleXSetY(settingsPanel, 0f, 0.35f, y, h);
		list.String = "North";
		list.String = "East";
		list.String = "South";
		list.String = "West";
		list.String = "Floor";
		list.String = "Aerial";
		list.String = "Split";
		list.String = "Off";
		list.String = "Manual";
		list.String = configuration[parameter].value;
		
		parameter = "monitorDistance";
		slider = new SliderField(NormalCallback, parameter, configuration[parameter].value); fields[parameter] = slider;
		slider.ScaleXSetY(settingsPanel, 0.35f, 1f, y, h);
		slider.Configure(5, 50, 4);
		slider.Prefix = "Distance to monitor (cm): ";
		
		y -= h;
		parameter = "azimuthView";
		slider = new SliderField(NormalCallback, parameter, configuration[parameter].value); fields[parameter] = slider;
		slider.ScaleXSetY(settingsPanel, 0f, 1f, y, h);
		slider.Configure(-180, 180, 0);
		slider.Prefix = "Azimuth (degrees): ";
		
		// Motion Settings.
		y -= t;
		parameter = "motionSettings";
		label = new LabelField(ToggleCallback, parameter, configuration[parameter].value); fields[parameter] = label;
		label.ScaleXSetY(settingsPanel, 0f, 1f, y, h);
		label.Prefix = "Motion Settings";
		
		// y -= h;
		// parameter = "autoAlign";
		// button = new ButtonField(ToggleCallback, parameter, configuration[parameter].value); fields[parameter] = button;
		// button.ScaleXSetY(settingsPanel, 0.75f, 1f, y, h);
		// button.Prefix = "Auto-align: ";
		// button.Rewrite("0", "OFF");
		// button.Rewrite("1", "ON");
		
		// parameter = "rotationGain";
		// slider = new SliderField(NormalCallback, parameter, configuration[parameter].value); fields[parameter] = slider;
		// slider.ScaleXSetY(settingsPanel, 0f, 0.75f, y, h);
		// slider.Configure(-2, 2, 4);
		// slider.Prefix = "Rotation gain: ";
		
		y -= h;
		parameter = "xGain";
		slider = new SliderField(NormalCallback, parameter, configuration[parameter].value); fields[parameter] = slider;
		slider.ScaleXSetY(settingsPanel, 0f, 1f, y, h);
		slider.Configure(-2, 2, 4);
		slider.Prefix = "X gain: ";
		
		y -= h;
		parameter = "yGain";
		slider = new SliderField(NormalCallback, parameter, configuration[parameter].value); fields[parameter] = slider;
		slider.ScaleXSetY(settingsPanel, 0f, 1f, y, h);
		slider.Configure(-2, 2, 4);
		slider.Prefix = "Y gain: ";
		
		// Triggers.
		y -= t;
		parameter = "triggerSettings";
		label = new LabelField(ToggleCallback, parameter, configuration[parameter].value); fields[parameter] = label;
		label.ScaleXSetY(settingsPanel, 0f, 1f, y, h);
		label.Prefix = "Trigger Settings";
		
		y -= h;
		parameter = "trialDuration";
		slider = new SliderField(NormalCallback, parameter, configuration[parameter].value); fields[parameter] = slider;
		slider.ScaleXSetY(settingsPanel, 0f, 0.75f, y, h);
		slider.Configure(0f, 2f, 4);
		slider.Prefix = "Trial duration (s): ";
		
		parameter = "setTrial";
		button = new ButtonField(NormalCallback, parameter, configuration[parameter].value); fields[parameter] = button;
		button.ScaleXSetY(settingsPanel, 0.75f, 1f, y, h);
		button.Prefix = "Trigger";
		button.Rewrite("0", "");
		button.Rewrite("1", "");
		
		y -= h;
		parameter = "triggerDuration";
		slider = new SliderField(NormalCallback, parameter, configuration[parameter].value); fields[parameter] = slider;
		slider.ScaleXSetY(settingsPanel, 0f, 0.50f, y, h);
		slider.Configure(0f, 2f, 4);
		slider.Prefix = "Trigger duration (s): ";
		
		parameter = "triggerPin";
		input = new TextField(NormalCallback, parameter, configuration[parameter].value); fields[parameter] = input;
		input.ScaleXSetY(settingsPanel, 0.50f, 0.75f, y, h);
		input.Hint = "Pin";
		
		parameter = "setTrigger";
		button = new ButtonField(NormalCallback, parameter, configuration[parameter].value); fields[parameter] = button;
		button.ScaleXSetY(settingsPanel, 0.75f, 1f, y, h);
		button.Prefix = "Trigger";
		button.Rewrite("0", "");
		button.Rewrite("1", "");
		
		
		// Miscellaneous Settings.
		y -= t;
		parameter = "miscellaneousSettings";
		label = new LabelField(ToggleCallback, parameter, configuration[parameter].value); fields[parameter] = label;
		label.ScaleXSetY(settingsPanel, 0f, 1f, y, h);
		label.Prefix = "Miscellaneous Settings";
		
		y -= h;
		parameter = "setTriggerOut";
		button = new ButtonField(NormalCallback, parameter, configuration[parameter].value); fields[parameter] = button;
		button.ScaleXSetY(settingsPanel, 0f, 1f, y, h);
		button.Prefix = "Trigger out: ";
		button.Rewrite("0", "OFF");
		button.Rewrite("1", "ON");
		
		y -= h;
		parameter = "userEntry";
		input = new TextField(NullCallback, parameter, configuration[parameter].value); fields[parameter] = input;
		input.ScaleXSetY(settingsPanel, 0f, 0.75f, y, h);
		input.Hint = "Notes";
		
		parameter = "setUserEntry";
		button = new ButtonField(NormalCallback, parameter, configuration[parameter].value); fields[parameter] = button;
		button.ScaleXSetY(settingsPanel, 0.75f, 1f, y, h);
		button.Prefix = "Enter";
		
		y -= h;
		parameter = "execute";
		input = new TextField(NormalCallback, parameter, configuration[parameter].value); fields[parameter] = input;
		input.ScaleXSetY(settingsPanel, 0f, 0.75f, y, 4f*h);
		input.Hint = "Commands";
		input.MultiLine = true;
		
		y -= 0f;
		parameter = "setExecute";
		button = new ButtonField(NormalCallback, parameter, configuration[parameter].value); fields[parameter] = button;
		button.ScaleXSetY(settingsPanel, 0.75f, 1f, y, 4f*h);
		button.Prefix = "Enter";
		
		y -= 4f*h;
		parameter = "logFile";
		button = new ButtonField(NormalCallback, parameter, configuration[parameter].value); fields[parameter] = button;
		button.ScaleXSetY(settingsPanel, 0f, 1f, y, h);
		button.Prefix = "Log file";
		
		y -= h;
		parameter = "resetSettings";
		button = new ButtonField(NormalCallback, parameter, configuration[parameter].value); fields[parameter] = button;
		button.ScaleXSetY(settingsPanel, 0f, 1f, y, h);
		button.Prefix = "Reset settings";
		button.Rewrite("0", "");
		button.Rewrite("1", "");
		
		// if (Tools.IsWindows) {
			// y -= h;
			// parameter = "flexKeyboard";
			// button = new ButtonField(ToggleCallback, parameter, configuration[parameter].value); fields[parameter] = button;
			// button.ScaleXSetY(settingsPanel, 0f, 1f, y, h);
			// button.Prefix = "Keyboard: ";
			// button.Rewrite("0", "OFF");
			// button.Rewrite("1", "Auto");
		// }
		
		y -= h;
		parameter = "about";
		button = new ButtonField(NormalCallback, parameter, configuration[parameter].value); fields[parameter] = button;
		button.ScaleXSetY(settingsPanel, 0f, 1f, y, h);
		button.Prefix = "About";
		button.Rewrite("0", "");
		button.Rewrite("1", "");
		
		y -= h;
		RectTransform rect = settingsPanel.GetComponent<RectTransform>();
		rect.sizeDelta = new Vector2(rect.sizeDelta.x, -y + 2f);
		
		// Hidden parameters.
		foreach (string hidden in configuration.Keys) {
			if (!fields.ContainsKey(hidden))
				fields[hidden] = new Field(NormalCallback, hidden, configuration[hidden].value);
		}
	}
	
	/* Client:
	 *	Send everyone's state to monitors.
	 */
	void PlayerForward(string instruction, string playerId) {
		if (Global.Network.RolePlayer)
			Global.Network.Send(Network.Recipients.Monitors, playerId, instruction);
	}
	
	void ForwardPosition(string playerId) {
		Player player = players[playerId];
		string lx = player.Position.x.ToString("#0.0000");
		string lz = player.Position.z.ToString("#0.0000");
		string ly = player.Position.y.ToString("#0.0000");
		string position = lx + "," + lz + "," + ly;
		PlayerForward("position," + position + ";", playerId);
	}
	
	void ForwardRotation(string playerId) {
		Player player = players[playerId];
		string rx = player.Euler.x.ToString("#0.0000");
		string rz = player.Euler.z.ToString("#0.0000");
		string ry = Tools.Mirror(player.Euler.y).ToString("#0.0000");
		string rotation = rx + "," + rz + "," + ry;
		PlayerForward("rotation," + rotation + ";", playerId);
	}
	
	bool PlayerCallback(string parameter, string values, string playerId) {
		bool success = true;
		string parsing = values;
		try {
			Player player;
			if (playerId.Equals("change") || playerId.Equals("quiet"))
				playerId = "default";
			if (players.ContainsKey(playerId)) {
				player = players[playerId];
			} else {
				GameObject gObj = (GameObject) Instantiate(Resources.Load("3D/Player"), Global.Player.Position, Global.Player.Rotation);
				player = gObj.AddComponent<Player>();
				players[playerId] = player;
			}
			switch (parameter) {
				case "position":
					float lx = player.Position.x;
					float.TryParse(Tools.Parse(ref parsing), out lx);
					float lz = player.Position.z;
					float.TryParse(Tools.Parse(ref parsing), out lz);
					float ly = player.Position.y;
					float.TryParse(Tools.Parse(ref parsing), out ly);
					Vector3 positionTarget = new Vector3(lx, ly, lz);
					Global.Player.transform.localPosition = positionTarget;
					break;
				case "rotation":
					float rx = 0f;
					float rz = 0f;
					float ry = 0f;
					success = float.TryParse(Tools.Parse(ref parsing), out rz);
					success = float.TryParse(Tools.Parse(ref parsing), out rx);
					success = float.TryParse(Tools.Parse(ref parsing), out ry);
					ry = Tools.Mirror(ry);
					Global.Player.transform.localEulerAngles = new Vector3(rx, ry, rz);
					break;
				case "linearSpeed":
					lBrakeNetwork.Reset();
					float dlx = lSpeedNetwork.x;
					float.TryParse(Tools.Parse(ref parsing), out dlx);
					float dlz = lSpeedNetwork.z;
					float.TryParse(Tools.Parse(ref parsing), out dlz);
					float dly = lSpeedNetwork.y;
					float.TryParse(Tools.Parse(ref parsing), out dly);
					lSpeedNetwork = new Vector3(dlx, dly, dlz);
					break;
				case "angularSpeed":
					rBrakeNetwork.Reset();
					float drx = rSpeedNetwork.x;
					float.TryParse(Tools.Parse(ref parsing), out drx);
					float drz = rSpeedNetwork.z;
					float.TryParse(Tools.Parse(ref parsing), out drz);
					float dry = rSpeedNetwork.y;
					if (float.TryParse(Tools.Parse(ref parsing), out dry))
						dry *= -1;
					rSpeedNetwork = new Vector3(drx, dry, drz);
					break;
			}
		} catch {
			success = false;
		}
		// Debug.Log(success + " " + playerId + " " + parameter + " " + values);
		return success;
	}
	
	void NullCallback(Field field) {
	}
	
	void NormalCallback(Field field) {
		Callback(field.Name, field.String, "change");
	}
	
	void ToggleCallback(Field field) {
		if (field.Equals("1"))
			Callback(field.Name, "0", "change");
		else if (field.Equals("0"))
			Callback(field.Name, "1", "change");
	}
	
	public void TrueCallback(string parameter) {
		Callback(parameter, "1", "change");
	}
	
	public void FalseCallback(string parameter) {
		Callback(parameter, "0", "change");
	}
	
	public void ToggleCallback(string parameter) {
		if (fields[parameter].Equals("1")) {
			Callback(fields[parameter].Name, "0", "change");
		} else if (fields[parameter].Equals("0")) {
			Callback(fields[parameter].Name, "1", "change");
		}
	}
	
	enum Option {
		True,
		False,
		Neutral
	}
	
	bool TestOption(bool test, Option option) {
		return (option == Option.True || test) && option != Option.False;
	}
	
	public bool Callback(string parameter, string value, string option) {
		parameter = parameter.Trim();
		value = Tools.Trim(value);
		string parsed = "";
		string parsing = "";
		bool success = true;
		float number = 0f;
		float[] numbers = new float[]{0f,0f,0f};
		float timestamp = Elapsed;
		if (fields.ContainsKey(parameter)) {
			// Forward or trigger from configuration.
			bool forward = configuration[parameter].send;
			bool trigger = configuration[parameter].trigger;
			// Log from configuration as long as role is player.
			bool log = configuration[parameter].log && Global.Network.RolePlayer;
			// Change when trigger or value change.
			bool change = trigger || !value.Equals(fields[parameter].String);
			// Modify preferences on change.
			bool save = configuration[parameter].save;
			// Initiate routine only if we are the player or commanded by the player.
			bool initiate = Global.Network.RolePlayer || option == "default";
			
			Option forceLog = Option.Neutral;
			Option forceChanged = Option.Neutral;
			Option forceForward = Option.Neutral;
			Option forceSave = Option.Neutral;
			switch (option) {
				case "default":
					if (Global.Network.RoleMonitor) {
						// Messages from player are not logged or forwarded.
						forceLog = Option.False;
						forceForward = Option.False;
						// Network does not modify local preferences.
						forceSave = Option.False;
					}
					break;
				case "change":
					// Force change to enable potential forward.
					forceChanged = Option.True;
					break;
				case "quiet":
					// Disable log and forward and force change.
					forceLog = Option.False;
					forceChanged = Option.True;
					forceForward = Option.False;
					forceSave = Option.False;
					break;
			}
			
			// Function overrides.
			log = TestOption(log, forceLog);
			change = TestOption(change, forceChanged);
			forward = TestOption(forward, forceForward);
			save = TestOption(save, forceSave);
			
			try {
				if (change) {
					switch (parameter) {
						case "position":
						case "rotation":
							log &= option.Equals("default");
							success = PlayerCallback(parameter, value, option);
							PlayerForward(parameter + "," + value, option);
							break;
						case "linearSpeed":
						case "angularSpeed":
							log &= option.Equals("default");
							success = PlayerCallback(parameter, value, option);
							break;
						case "maze":
							success = LoadMaze(value);
							break;
						case "objects":
							success = Objects(value);
							break;
						case "transform":
							success = TransformObject(value);
							break;
						case "sessionId":
							break;
						case "deviceMode":
							success = DeviceMode(value);
							break;
						case "setMode":
							switch (fields["deviceMode"].String) {
								case "Control":
									Global.Menu.Show(clientMenu);
									break;
								case "Monitor":
									Global.Menu.Show(monitorMenu);
									break;
							}
							break;
						
						case "deviceSettings":
							break;
							
						case "viewSettings":
							break;
							
						case "view":
							success = View(value);
							break;
							
						case "azimuthView":
							cameras["Manual"].transform.localEulerAngles = new Vector3(-fields["altitudeView"].Number, float.Parse(value), 0f);
							break;
						case "altitudeView":
							cameras["Manual"].transform.localEulerAngles = new Vector3(-float.Parse(value), fields["azimuthView"].Number, 0f);
							break;
						case "cameraPosition":
							string[] cpParts = Tools.Split(value);
							if (cpParts.Length == 3) {
								float cx;
								float cy;
								float cz;
								success &= float.TryParse(cpParts[0], out cx);
								success &= float.TryParse(cpParts[1], out cz);
								success &= float.TryParse(cpParts[2], out cy);
								cameras["Manual"].transform.localPosition = new Vector3(cx, cy, cz);
							}
							break;
							
						case "monitorDistance":
							number = fields["monitorDistance"].Number;
							success = float.TryParse(value, out number);
							success &= number >= 1f && number <= 50f;
							if (success) {
								fields["monitorDistance"].String = value;
								UpdateView(true);
							}
							break;
						case "motionSettings":
							break;
						case "virtualRotation":
							success = value == "1" || value == "0";
							if (success) {
								if (value == "0")
									relativeCameras.rotation = Quaternion.Euler(0f, 0f, 0f);
								// Update before calling configure.
								fields["virtualRotation"].String = value;
							}
							break;
						case "autoAlign":
							success = value == "1" || value == "0";
							if (success) {
								// Update before calling configure.
								fields["autoAlign"].String = value;
							}
							break;
						case "rotationGain":
						case "brakeDuration":
							number = float.Parse(value);
							lBrake.Duration = number;
							break;
						case "xGain":
						case "yGain":
							break;
						case "linearSpeedLimits":
							string[] lslParts = Tools.Split(value);
							if (lslParts.Length == 4) {
								float[] tmp = new float[4];
								for (int i = 0; i < 4; i++)
									tmp[i] = float.Parse(lslParts[i]);
								// Copy all at once only if parsing of all parts is successful.
								lSpeedLimits = tmp;
							} else {
								success = false;
							}
							break;
						case "triggerSettings":
							break;
						case "trialDuration":
							number = float.Parse(value);
							break;
						case "setTrial":
							success = value == "1" || value == "0";
							if (success && value == "1")
								success = Callback("trial", fields["trialDuration"].String, "change");
							break;
						case "triggerDuration":
							number = float.Parse(value);
							break;
						case "triggerPin":
							number = int.Parse(value);
							success = number > 1 & number < 65;
							break;
						case "setTrigger":
							success = value == "1" || value == "0";
							if (success && value == "1")
								success = Callback("trigger", fields["triggerPin"].String + "," + fields["triggerDuration"].String, "change");
							break;
						case "spawnPosition":
							parsing = value;
							for (int i = 0; i < 3 && success; i++) {
								if (Tools.Parse(ref parsing, ref parsed)) {
									parsed = parsed.Trim();
									if (!parsed.Equals("")) {
										if (float.TryParse(parsed, out number))
											numbers[i] = number;
										else
											success = false;
									}
								}
							}
							if (success)
								spawnPosition = new Vector3(numbers[0], numbers[2], numbers[1]);
							break;
						case "spawnRotation":
							parsing = value;
							parsed = "";
							for (int i = 0; i < 3 && success; i++) {
								if (Tools.Parse(ref parsing, ref parsed)) {
									parsed = parsed.Trim();
									if (!parsed.Equals("")) {
										if (float.TryParse(parsed, out number))
											numbers[i] = number;
										else
											success = false;
									}
								}
							}
							if (success) {
								spawnRotation = new Vector3(numbers[1], numbers[2], numbers[0]);
								spawnRotation.y = Tools.Mirror(spawnRotation.y);
							}
							break;
						
						case "tone":
							parsing = value;
							float toneFrequency = 0f;
							float toneDuration = 0f;
							float.TryParse(Tools.Parse(ref parsing), out toneFrequency);
							float.TryParse(Tools.Parse(ref parsing), out toneDuration);
							if (success && initiate)
								Tools.Tone(toneFrequency, toneDuration);
							break;
						case "trigger":
							parsing = value;
							int valvePin = 0;
							float valveDuration = 0f;
							int.TryParse(Tools.Parse(ref parsing), out valvePin);
							float.TryParse(Tools.Parse(ref parsing), out valveDuration);
							success = valvePin == 0 || valveDuration >= 0 && valveDuration >= 0;
							if (success && initiate) {
								if (valvePin > 0)
									bridge.SetPulse(valvePin, 1, 0, Mathf.RoundToInt(1e6f * valveDuration), 1);
							}
							break;
						case "pickup":
							break;
							
						case "miscellaneousSettings":
							break;
						
						case "trial":
							float trialDuration = float.Parse(value);
							bool trialStep = Mathf.Approximately(trialDuration, -1f);
							success = trialDuration >= 0 || trialStep;
							if (success && initiate) {
								if (trialStep) {
									if (status["trial"] == 1)
										Trial(2);
								} else {
									Trial(trialDuration, 1);
								}
							}
							break;
						case "triggerOutDuration":
							break;
						case "setTriggerOut":
							if (status["triggerOut"] == 0) {
								Callback("triggerOut", fields["triggerOutDuration"].String, "change");
							} else {
								Callback("triggerOut", "0", "change");
							}
							break;
						case "triggerOut":
							float triggerOutDuration = float.Parse(value);
							bool triggerOutStep = Mathf.Approximately(triggerOutDuration, -1f);
							success = triggerOutDuration >= 0 || triggerOutStep;
							if (success && initiate) {
								if (triggerOutStep) {
									if (status["triggerOut"] == 1)
										TriggerOut(2);
									else if (status["triggerOut"] == 2)
										TriggerOut(3);
								} else if (Mathf.Approximately(triggerOutDuration, 0f)) {
									TriggerOut(0);
								} else {
									TriggerOut(triggerOutDuration, 1);
								}
								fields["setTriggerOut"].Label.text = fields["setTriggerOut"].Prefix + (status["triggerOut"] == 0 ? "OFF" : "ON");
							}
							
							break;
						case "userEntry":
							if (option.Equals("default")) {
								if (Global.Network.RolePlayer) {
									// Player does not forward userEntry.
									forward = false;
									// Unescape data forwarded from a monitor.
									string text = Tools.UnEscape(value);
									// Verify it is well formatted.
									string corrected = CorrectEntry(text);
									success = text.Length > 0 && text.Equals(corrected);
									if (success) {
										// Switch on logger and make entry a literal string.
										loggerOn = true;
										value = "\"" + text + "\"";
									} else {
										Debug.Log(text + "|" + corrected);
									}
								}
							} else {
								// Event from GUI.
								success = value.Length > 0 && value.Equals(CorrectEntry(value));
								if (success) {
									string text = value;
									if (Global.Network.RolePlayer) {
										// Player does not forward userEntry.
										forward = false;
										// Switch on logger and make entry a literal string.
										loggerOn = true;
										value = "\"" + text + "\"";
									} else {
										// Prepare for forwarding: Escape.
										value = Tools.Escape(text);
									}
								}
							}
							break;
						case "setUserEntry":
							string entry = fields["userEntry"].String;
							success = Callback("userEntry", entry, "change");
							if (success)
								fields["userEntry"].String = "";
							else
								fields["userEntry"].String = CorrectEntry(entry);
							break;
						case "execute":
							//commandsUI.text = value;
							break;
						case "setExecute":
							//fields["execute"].String = commandsUI.text;
							ExecuteGUI(fields["execute"].String);
							break;
						case "logFile":
							ShowLog();
							break;
						case "resetSettings":
							if (value == "1")
								Defaults("change");
							break;
						case "about":
							ShowMessage(aboutMessage);
							break;
							
						case "filename":
							value = value.Trim();
							break;
						case "exit":
							ShowMessage("<b>Closing, please wait...</b>");
							Global.Instance.Quit();
							break;
						case "help":
							ShowMessage(helpMessage);
							break;
						case "flexKeyboard":
							success = value == "1" || value == "0";
							break;
						// Not parameters.
						case "elapsed":
							Elapsed = float.Parse(value);
							break;
						
						default:
							success = false;
							break;
					}
					if (success) {
						// Log changes.
						if (log) {
							Log(timestamp.ToString("#0.0000") + "," + parameter + "," + value);
						}
						// Forward to monitors or to player.
						if (forward)
							Forward(parameter, value);
						// Update GUI. Triggers never change values automatically.
						if (change && !trigger) {
							Set(parameter, value);
						}
						if (save)
							PlayerPrefs.SetString(parameter, value);
					}
				}
			} catch (Exception e) {
				// Debug.Log(string.Format("Error: {0} << {1}\nMessage: {2}", parameter, value, e.Message));
				success = false;
			}
		} else {
			success = false;
		}
		if (!success) {
			Debug.Log(string.Format("Failed to execute Callback({0}, {1}, {2})", parameter, value, option));
		}
		// Debug.Log(string.Format("Callback({0}, {1}, {2})", parameter, value, option));
		return success;
	}
	
	public void Set(string parameter, string value) {
		if (fields.ContainsKey(parameter))
			fields[parameter].String = value;
	}
	
	public string Get(string parameter) {
		return fields[parameter].String;
	}
	
	public void Forward(string parameter, string value) {
		if (Global.Network.RolePlayer)
			Global.Network.Send(Network.Recipients.Monitors, "default", parameter + "," + value + ";");
		else if (Global.Network.RoleMonitor)
			Global.Network.Send(Network.Recipients.Player, "default", parameter + "," + value + ";");
	}
	
	string CorrectEntry(string value) {
		string corrected = Regex.Replace(value, "[^a-zA-Z0-9 ':;,<>=_!@#$%&/\\|\\.\\*\\-\\+\\?\\(\\)\\[\\]\\{\\}]+", " ");
		return corrected;
	}
	
	bool TransformObject(string instruction) {
		bool success = true;
		string[] parts = instruction.Split(',');
		if (parts.Length == 10) {
			string groupId = parts[0].Trim();
			if (objects.ContainsKey(groupId)) {
				float[] floats = new float[9];
				for (int i = 0; i < 9; i++)
					success &= float.TryParse(parts[i + 1], out floats[i]);
				if (success) {
					foreach (GameObject gObj in objects[groupId]) {
						Transform transform = gObj.transform;
						transform.localPosition = new Vector3(floats[0], floats[2], floats[1]);
						transform.localScale = new Vector3(floats[3], floats[5], floats[4]);
						transform.localEulerAngles = new Vector3(floats[6], floats[8], 90f - floats[7]);
					}
				}
			} else {
				success = false;
			}
		} else if (parts.Length == 1 && parts[0].Trim() == "") {
			success = true;
		}
		return success;
	}
	
	bool Objects(string instruction) {
		bool success = false;
		if (ObjectsFunction(instruction, false))
			success = ObjectsFunction(instruction, true);
		return success;
	}
	
	/*
		objects;
			Remove all objects.
		objects, <group-id>;
			Remove all objects within group id.
		objects, <group-id>, <shape-settings>, <rendering-settings>, <interaction-settings>;
			Create/replace all objects within a given group id. Each object require three major group of settings to define its shape, rendering, and interaction. Concatenate these three settings for each new object.
		
		<group-id>
			A label that gives access to these objects. Reusing this label causes changes to these objects exclusively.
		<shape-settings>
			<shape>, <x>, <y>, <z>, <dx>, <dy>, <dz>, <rx>, <ry>, <rz>, 
			The shape of the new object (cone, cube, cylinder, disk, gaussian, sphere) followed by its position, dimensions and rotation.
			<bundleName/objectName>
				An object from a bundle.
		<rendering-settings>
			invisible, 
				Object is only visible in aerial view.
			color, <R>, <G>, <B>
				Object is painted with the RGB color code (color values range from 0 to 1).
			<image-filename>, <nx>, <ny>
				An image from the Resources folder of SmoothWalk is loaded and rendered in the object <nx> times horizontally and <ny> times vertically.
			<material-name>
				The object reuses a material from the maze. Available materials: wall.
			fixed-grating, <ncycles>, <rotation>, <aspect-ratio>
				The object displays a grating with a given number of cycles and rotation. The vertical scale of the image depends on the aspect-ratio (range from 0 to 1). Gratings are computed for the x-z face of the object.
			auto-grating, <frequency>, <rotation>
				A grating is continously generated to maintain a constant number of cycles per degree regardless of the separation between the subject and the object. Gratings are computed for the x-z face of the object.
				The choice of frequency and rotation can impact performance significantly: Lower frequencies are computationally expensive, particularly at an angle. Preferentially, use higher frequencies (e.g. higher than 1 cycles/degree) over lower frequencies and horizontal or vertical bands over rotations. To obtain a rotated grating, define a horizontal or vertical grating (0 or 90 degrees) in a disk rotated around the y axis. It is much faster to produce a horizontal grating in a rotated disk than a rotated grating in a straight disk.
		<interaction-options>
			obstacle
				The object is solid and the subject cannot go thru it, neither will trigger further events. Use this option to create walls, for example.
			pickup, <event-label>, <trigger-pin>, <trigger-delay>, <trigger-duration>, <tone-frequency>, <tone-duration>, <retrigger-interval>, <trigger-probability>
				An event is triggered when the subject goes thru the object: An entry created in the log file with a given label, output pin is switched on for a given duration if the player stays within the object for a given delay, and a tone is played with a given frequency and duration. This event can only retrigger if it had not previously triggered within a given interval and if the probability is favorable. Set event label to trial to issue a new trial on trigger. Set the pin number to zero if you do not want to trigger a hardware change. Set frequency and duration to zero if you do not want to play a tone. Set the retrigger interval to -1 if you only want to permit one trigger per trial.
	*/
	bool ObjectsFunction(string instruction, bool execute) {
		bool success = true;
		bool isShape = false;
		bool isTrigger = false;
		string renderChoice = "";
		string typeChoice = "";
		
		PickupData pickupData = null;
		string shape = "";
		string filename = "";
		string assetPath = "";
		string groupId = "";
		string shapes = "cone|cube|cylinder|disk|gaussian|plane|sphere";
		string materials = "wall";
		string materialName = "";
		int  pos = 0;
		float  x = 0f;
		float  y = 0f;
		float  z = 0f;
		float dx = 5f;
		float dy = 5f;
		float dz = 5f;
		float rx = 0f;
		float ry = 0f;
		float rz = 0f;
		float  r = 0f;
		float  g = 0f;
		float  b = 0f;
		float nx = 1f;
		float ny = 1f;
		float gf = 0f;
		float gr = 0f;
		float ga = 0f;
		int gn = 0;
		Queue<float> starryParams = new Queue<float>();
		
		// Tag to filename of 3D resource.
		Dictionary<string,string> filenames = new Dictionary<string,string>(){
			{"cone", "Cone"},
			{"cube", "Cube"},
			{"cylinder", "Cylinder"},
			{"disk", "Disk"},
			{"gaussian", "Gaussian"},
			{"plane", "Plane"},
			{"sphere", "Sphere"}
		};
		
		// CSV.
		string[] parts = Tools.Split(instruction);
		
		if (parts.Length == 0) {
			// Group is empty when command is "objects;"
			groupId = "";
		} else {
			// Group is specified when command is "objects, groupId, <...>" or "objects, groupId;"
			groupId = parts[pos++].Trim();
		}
		
		bool changed = true;
		// Update lists and objects if requested to execute.
		if (execute) {
			// Requested to clear all objects.
			if (groupId.Equals("")) {
				foreach (GameObject gObj in GameObject.FindGameObjectsWithTag("object"))
					GameObject.Destroy(gObj);
				// Clear object references.
				objects.Clear();
				// Clear commands.
				objectCommands.Clear();
			// Command applies to existing groupId.
			} else if (objects.ContainsKey(groupId)) {
				// Command did not change.
				if (objectCommands[groupId].Equals(instruction)) {
					changed = false;
				// Command changed.
				} else {
					// Clear all within groupId.
					foreach (GameObject obj in objects[groupId])
						GameObject.Destroy(obj);
					// Clear object references in groupId.
					objects[groupId].Clear();
				}
				// Add/replace command for groupId.
				objectCommands[groupId] = instruction;
			} else {
				// Add/replace command for groupId.
				objectCommands[groupId] = instruction;
			}
		}
		
		if (changed && parts.Length > 1) {
			do {
				// Shape settings.
				shape = parts[pos++].Trim();
				if (Regex.IsMatch(shapes, @"\b" + shape + @"\b")) {
					isShape = true;
				} else if (shape.Equals("prefab")) {
					filename = parts[pos++].Trim();
					assetPath = parts[pos++].Trim();
				} else {
					success = false;
					return success;
				}
				
				// Size settings.
				success &= float.TryParse(parts[pos++], out  x);
				success &= float.TryParse(parts[pos++], out  z);
				success &= float.TryParse(parts[pos++], out  y);
				success &= float.TryParse(parts[pos++], out dx) && dx >= 0f;
				success &= float.TryParse(parts[pos++], out dz) && dz >= 0f;
				success &= float.TryParse(parts[pos++], out dy) && dy >= 0f;
				success &= float.TryParse(parts[pos++], out rx);
				success &= float.TryParse(parts[pos++], out rz);
				success &= float.TryParse(parts[pos++], out ry);
				rx = -rx;
				ry = 180f - ry;
				rz = -rz;
				
				// Render settings.
				if (isShape) {
					renderChoice = parts[pos++].Trim();
					if (renderChoice.Equals("invisible")) {
					} else if (renderChoice.Equals("auto-grating")) {
						success &= float.TryParse(parts[pos++], out gf) && gf >= 0;
						success &= float.TryParse(parts[pos++], out gr);
					} else if (renderChoice.Equals("fixed-grating")) {
						success &=   int.TryParse(parts[pos++], out gn) && gn > 0;
						success &= float.TryParse(parts[pos++], out gr);
						success &= float.TryParse(parts[pos++], out ga) && ga > 0 && ga <= 1;
					} else if (renderChoice.Equals("image")) {
						filename = parts[pos++].Trim();
						success &= float.TryParse(parts[pos++], out nx) && nx > 0;
						success &= float.TryParse(parts[pos++], out ny) && ny > 0;
					} else if (renderChoice.Equals("image-embedded")) {
						filename = parts[pos++].Trim();
						success &= float.TryParse(parts[pos++], out nx) && nx > 0;
						success &= float.TryParse(parts[pos++], out ny) && ny > 0;
					} else if (renderChoice.Equals("color")) {
						success &= float.TryParse(parts[pos++], out r) && r >= 0 && r <= 1;
						success &= float.TryParse(parts[pos++], out g) && g >= 0 && g <= 1;
						success &= float.TryParse(parts[pos++], out b) && b >= 0 && b <= 1;
					} else if (renderChoice.Equals("starry")) {
						for (int p = 0; p < 12; p++) {
							float param;
							success &= float.TryParse(parts[pos++], out param);
							starryParams.Enqueue(param);
						}
					} else if (renderChoice.Equals("material")) {
						materialName = parts[pos++].Trim();
						success = Regex.IsMatch(materials, @"\b" + materialName + @"\b");
					} else {
						success = false;
						return success;
					}
				}
				
				// Interaction settings.
				typeChoice = parts[pos++].Trim();
				if (typeChoice.Equals("pickup")) {
					isTrigger = true;
					float number = 0f;
					pickupData = new PickupData();
					pickupData.Tag = parts[pos++].Trim();
					if (Regex.IsMatch(pickupData.Tag, @"[^_a-zA-Z0-9&\.\-\+ ]"))
						success = false;
					pickupData.Pin = int.Parse(parts[pos++]); success &= pickupData.Pin >= 0;
					success &= float.TryParse(parts[pos++], out number) && number >= 0; pickupData.Delay = number;
					float[] numbers = new float[2]{0,0};
					success &= Tools.ParseRange(parts[pos++].Trim(new char[]{' ', '[', ']'}), ref numbers) && numbers[0] >= 0f; pickupData.DurationRange = numbers;
					success &= float.TryParse(parts[pos++], out number); pickupData.Tone[0] = number;
					success &= float.TryParse(parts[pos++], out number); pickupData.Tone[1] = number;
					success &= float.TryParse(parts[pos++], out number); pickupData.Interval = number;
					success &= float.TryParse(parts[pos++], out number) && number >= 0f && number <= 1f; pickupData.Probability = number;
				} else if (typeChoice.Equals("obstacle")) {
					isTrigger = false;
				} else if (typeChoice.Equals("stop")) {
					isTrigger = false;
				} else {
					success = false;
					return success;
				}
				
				if (success && execute) {
					GameObject child;
					GameObject father;
					if (isShape) {
						father = (GameObject) Instantiate(Resources.Load("3D/" + filenames[shape]));
						child = father.transform.GetChild(0).gameObject;
						if (typeChoice.Equals("stop")) {
							Block block = child.GetComponent<Block>();
							Destroy(block);
						}
						father.tag = "object";
						father.transform.position = new Vector3(x, y, z);
						father.transform.localScale = new Vector3(dx, dy, shape.Equals("disk") ? father.transform.localScale.z : dz);
						father.transform.rotation = Quaternion.Euler(new Vector3(-rx, ry, 0f));
						child.transform.Rotate(0f, 0f, rz);
						if (!objects.ContainsKey(groupId))
							objects[groupId] = new List<GameObject>();
						objects[groupId].Add(father);
						
						if (shape.Equals("cube") && !isTrigger) {
							// Create edges.
							GameObject[] edges = new GameObject[4];
							for (int e = 0; e < edges.Length; e++) {
								edges[e] = GameObject.CreatePrimitive(PrimitiveType.Cube);
								edges[e].transform.parent = child.transform;
								edges[e].transform.rotation = child.transform.rotation;
								Destroy(edges[e].GetComponent<Renderer>());
							}
							float ldx = 0.1f/dx;
							// float ldy = 0.1f/dy;
							float ldz = 0.1f/dz;
							int p = 0;
							
							// Along y.
							edges[p].transform.localScale = new Vector3(ldx, 1f, ldz);
							edges[p++].transform.localPosition = new Vector3(+0.5f, 0f, +0.5f);
							
							edges[p].transform.localScale = new Vector3(ldx, 1f, ldz);
							edges[p++].transform.localPosition = new Vector3(+0.5f, 0f, -0.5f);
							
							edges[p].transform.localScale = new Vector3(ldx, 1f, ldz);
							edges[p++].transform.localPosition = new Vector3(-0.5f, 0f, +0.5f);
							
							edges[p].transform.localScale = new Vector3(ldx, 1f, ldz);
							edges[p++].transform.localPosition = new Vector3(-0.5f, 0f, -0.5f);
						}
					
						Texture2D texture;
						Material material = child.GetComponent<Renderer>().material;
						switch (renderChoice) {
							case "image-embedded":
								textureLoader.LoadResource(filename, new TextureData(material, true));
								material.mainTextureScale = new Vector2(nx, ny);
								break;
							case "image":
								textureLoader.Load(logsFolder + "/" + filename, new TextureData(material, true));
								material.mainTextureScale = new Vector2(nx, ny);
								break;
							case "color":
								// Assign colored texture.
								texture = new Texture2D(1, 1);
								Color color = new Color(r, g, b);
								texture.SetPixel(0, 0, color);
								texture.Apply();
								material.mainTexture = texture;
								break;
							case "auto-grating":
								child.GetComponent<Grating>().SetCyclesPerDegree(gf, gr*Mathf.Deg2Rad, 0f, 8192);
								break;
							case "fixed-grating":
								child.GetComponent<Grating>().SetCycles(gn, 32, gr*Mathf.Deg2Rad, 0f, ga);
								break;
							case "invisible":
								child.layer = LayerMask.NameToLayer("Aerial");
								break;
							case "starry":
								Starry starry = child.AddComponent<Starry>();
								starry.Setup(
									Mathf.RoundToInt(starryParams.Dequeue()),
									Mathf.RoundToInt(starryParams.Dequeue()),
									Mathf.RoundToInt(starryParams.Dequeue()),
									Mathf.RoundToInt(starryParams.Dequeue()),
									Mathf.RoundToInt(starryParams.Dequeue()),
									Mathf.RoundToInt(starryParams.Dequeue()),
									Mathf.RoundToInt(starryParams.Dequeue()),
									Mathf.RoundToInt(starryParams.Dequeue()),
									Mathf.RoundToInt(starryParams.Dequeue()),
									Mathf.RoundToInt(starryParams.Dequeue()),
									starryParams.Dequeue(),
									starryParams.Dequeue()
								);
								break;
							case "material":
								// if (materialName.Equals("wall"))
									// Tools.CopyMaterial(northLimit.gameObject, child);
								break;
						}
						SetColliderTriggerAndPickup(child, pickupData);
					} else {
						Vector3 scale = new Vector3(dx, dy, dz);
						Vector3 position = new Vector3(x, y, z);
						Vector3 rotation = new Vector3(rx, ry, rz);
						bundleLoader.Load(logsFolder + "/" + filename, assetPath, new BundleData(groupId, scale, position, rotation, pickupData, true));
					}
				}
			} while (success && pos < parts.Length);
		}
		return success;
	}
	
	void SetColliderTriggerAndPickup(GameObject asset, PickupData pickupData) {
		// Insert one pickup per collider, which triggers independently from one another.
		// Basic shapes are designed with one collider, hence one pickup.
		// User chooses number of shapes/colliders when producing prefabs in bundles, hence number of pickups per shape is variable.
		Collider[] colliders = asset.GetComponentsInChildren<Collider>();
		bool isTrigger = pickupData != null;
		foreach (Collider cder in colliders) {
			cder.isTrigger = isTrigger;
			if (isTrigger) {
				GameObject sub = cder.gameObject;
				Pickup pickup = sub.AddComponent<Pickup>();
				pickup.Pin = pickupData.Pin;
				pickup.Tag = pickupData.Tag;
				pickup.Delay = pickupData.Delay;
				pickup.DurationRange = pickupData.DurationRange;
				pickup.Tone = pickupData.Tone;
				pickup.Interval = pickupData.Interval;
				pickup.Probability = pickupData.Probability;
				pickup.Callback = PickupEvent;
			}
		}
	}
	
	void OnBundleLoadSuccess(string bundlePath, string assetPath, GameObject asset, object data) {
		BundleData parameters = (BundleData) data;
		asset.transform.localScale = parameters.Scale;
		asset.transform.localPosition = parameters.Position;
		asset.transform.localEulerAngles = parameters.Rotation;
		asset.tag = "object";
		if (!objects.ContainsKey(parameters.GroupId))
			objects[parameters.GroupId] = new List<GameObject>();
		objects[parameters.GroupId].Add(asset);
		SetColliderTriggerAndPickup(asset, parameters.PickupData);
	}
	
	void OnBundleLoadFail(string bundlePath, string assetPath, string message, object data) {
		BundleData parameters = (BundleData) data;
		if (parameters.Verbose)
			AppendMessage(message);
	}
	
	void OnTextureLoadSuccess(string imagePath, Texture2D texture, object data) {
		TextureData parameters = (TextureData) data;
		parameters.Target.mainTexture = texture;
	}
	
	void OnTextureLoadFail(string imagePath, string message, object data) {
		TextureData parameters = (TextureData) data;
		if (parameters.Verbose)
			AppendMessage(message);
	}
	
	bool VirtualRotation {
		get {
			return fields["virtualRotation"].Equals("1");
		}
		
		set {
			fields["virtualRotation"].String = value ? "1" : "0";
		}
	}
	
	public bool FlexKeyboard {
		get {
			return fields["flexKeyboard"].String == "1";
		}
	}
	
	void PickupEvent(Pickup pickup, Pickup.Events pickupEvent, float elapsed) {
		if (Global.Network.RolePlayer && collisions) {
			string tag = pickup.Tag;
			
			string enabled = (pickup.Enable ? "enabled" : "disabled");
			switch (pickupEvent) {
				case Pickup.Events.Enter:
					Callback("pickup", tag + ",enter," + enabled, "change");
					break;
				case Pickup.Events.Trigger:
					//Debug.Log("Trigger: " + tag);
					if (tag.Equals("trial"))
						Callback("trial", string.Format("{0:0.0000}", pickup.Duration), "change");
					Callback("trigger", string.Format("{0}, {1:0.0000}", pickup.Pin, pickup.Duration), "change");
					Callback("tone", string.Format("{0:0.0000}, {1:0.0000}", pickup.Tone[0], pickup.Tone[1]), "change");
					Callback("pickup", tag + ",trigger," + enabled, "change");
					break;
				case Pickup.Events.Exit:
					Callback("pickup", tag + ",exit," + enabled, "change");
					break;
				case Pickup.Events.Premature:
					Callback("pickup", tag + ",premature," + enabled, "change");
					break;
			}
		}
	}
	
	List<Tuple<float,string>> trials = new List<Tuple<float,string>>();
	public bool ExecuteGUI(string text) {
		string message = "<b>Executing instructions</b>\n";
		bool success = true;
		string[] parts = Regex.Split(text, @"([^\s]+)%");
		List<Tuple<float,string>> trials = new List<Tuple<float,string>>();
		// No probabilities.
		if (parts.Length == 1) {
			trials.Add(new Tuple<float, string>(1f, parts[0]));
		} else {
			// Either empty or before probability definitions.
			string trial = parts[0].Trim();
			if (trial.Length > 0) {
				message += "Global definitions\n";
				Execute(trial, ref message);
			}
			
			// Probabilities.
			for (int i = 1; i < parts.Length; i += 2) {
				float probability = 0f;
				if (float.TryParse(parts[i], out probability)) {
					trials.Add(new Tuple<float, string>(probability, parts[i+1]));
				} else {
					message += "Trial definition #" + (i/2+1) + "\n    Incorrect probability. Ignoring all settings within.\n";
					success = false;
				}
			}
		}
		
		for (int iTrial = 0; iTrial < trials.Count; iTrial++) {
			string trial = trials.ElementAt(iTrial).Item2;
			float probability = trials.ElementAt(iTrial).Item1;
			if (trials.Count > 1)
				message += string.Format("Trial definition #{0} ({1}%)\n", iTrial, probability);
			Execute(trial, ref message);
		}
		
		message += "\n";
		trials.Sort((a,b) => a.Item1.CompareTo(b.Item1));
		
		if (success)
			this.trials = trials;
		
		if (!success && trials.Count > 1)
			message += "Errors found. Trial based settings were not updated!";
		
		ShowMessage(message);
		// AppendMessage(message);
		return success;
	}
	
	public bool Execute(string trial) {
		string message = "";
		return Execute(trial, ref message);
	}
	
	public bool Execute(string trial, ref string message) {
		bool success = true;
		// Remove enter so that it doesn't split in multiple lines at logging.
		trial = Regex.Replace(trial, @"\s+", @" ");
		// Instructions are separated by semicolons.
		string[] list = Regex.Split(trial, @";");
		string messageOK = "";
		string messageFail = "";
		string messageIgnored = "";
		int n = list.Length;
		for (int l = 0; l < n - 1; l++) {
			// Retrieve command and fields.
			string remaining = list[l].Trim();
			if (remaining.Length > 0) {
				string parameter = Tools.Parse(ref remaining);
				string value = remaining;
				if (executeParameters.Contains(parameter)) {
					if (Callback(parameter, value, "change")) {
						messageOK += "    " + list[l] + "\n";
					} else {
						success = false;
						messageFail += "    " + list[l] + "\n";
					}
				} else {
					messageIgnored += "    " + list[l] + "\n";
					success = false;
				}
			}
		}
		string last = list[n-1].Trim();
		if (last.Length > 0) {
			success = false;
			messageFail += "    " + last + "\n";
		}	
		
		if (messageOK.Length > 0)
			message += "  Successful:\n" + messageOK;
		if (messageFail.Length > 0)
			message += "  Unsuccessful:\n" + messageFail;
		if (messageIgnored.Length > 0)
			message += "  Unrecognized:\n" + messageIgnored;
		return success;
	}
	
	void OnApplicationQuit() {
		// Save data one last time.
		if (loggerOn)
			logWriter.Flush();
		logWriter.Dispose();
		bridge.Dispose();
		
		//Hardware.UpdateMediaScanner2(fields["filename"].String);
		string[] files = System.IO.Directory.GetFiles(logsFolder, "*.csv");
		foreach (var file in files) {
			Hardware.UpdateMediaScanner2(file);
		}
		
		// Make Android file available for browsing using PTP/MTP.
		//#
		//try {
			//Hardware.UpdateMediaScanner2(fields["filename"].String);
		//} catch {}
	}
	
	public void ShowLog() {
		//#
		if (Hardware.IsAndroid) {
			// Remove confusing prefix path, not seen in PC.
			string folder = Regex.Replace(fields["filename"].String, "^(.*/Android)?", "Android");
			ShowMessage("<b>Location of the data log file</b>\n\"" + folder + "\"\nTo access this file in a computer: First exit this app then browse to the location shown above.");
		} else
			ShowMessage("<b>Location of the data log file</b>\n\"" + fields["filename"].String);
		try {
			ShowExplorer(logsFolder);
		} catch {}
		try {
			TextEditor te = new TextEditor();
			te.text = logsFolder; te.SelectAll(); te.Copy(); te.Delete();
		} catch {}
	}
	
	void ShowExplorer(string itemPath) {
		itemPath = itemPath.Replace(@"/", @"\");
		System.Diagnostics.Process.Start("explorer.exe", "/root," + itemPath);
	}
	
	public void ShowMessage(string message) {
		MainThread.Call(ShowMessageCallback, message);
	}
	
	public void AppendMessage(string message) {
		MainThread.Call(AppendMessageCallback, message);
	}
	
	void ShowMessageCallback(object obj) {
		string message = (string) obj;
		messageText.text = message;
	}
	
	void AppendMessageCallback(object obj) {
		string message = (string) obj;
		message += "\n\n" + messageText.text;
		int max = 4096;
		if (message.Length > max) {
			message = message.Substring(0, max);
			message += ".../ text clipped";
		}
		messageText.text = message;
	}
	
	string PlayerMessage {
		get {
			string message = "<b>Player mode</b>\nThis device controls the avatar in the virtual maze.\nMultiple monitors may be arranged around the player to display a greater field of view and create a more immersive environment.\n\n";
			if (Global.Network.Monitors.Count == 0) {
				message += "Enter the ID# of these monitors in <i>Target monitors</i>.";
			} else if (Global.Network.IDs.Count == 0) {
				message += "This device is not connected to the network.";
			} else if (Global.Network.IDs.Count == 1) {
				message += "In each monitor device defined in <i>Target monitors</i>, click <i>Monitor</i> until you reach the ID# of this device.";
			} else if (Global.Network.IDs.Count > 1) {
				message += "In each monitor device defined in <i>Target monitors</i>, click <i>Monitor</i> until you reach one of the ID#'s of this device.";
			}
			return message;
		}
	}
	
	string ID {
		get {
			string ids = "";
			List<string> IDs = Global.Network.IDs;
			if (IDs.Count == 0)
				ids = "Not connected";
			else if (IDs.Count == 1)
				ids = IDs.ElementAt(0);
			else
				ids = string.Join(" or ", IDs.ToArray());
			return ids;
		}
	}
	
	string Status {
		get {
			string status = "";
			if (Global.Network.RolePlayer) {
				int count = Global.Network.Monitors.Count;
				if (count > 0)
					status += "Sharing with " + count + " monitors";
				else
					status += "No monitors selected";
			} else if (Global.Network.RoleMonitor) {
				string choice = Global.Monitor.Choice;
				string player = Global.Network.Player;
				bool playerSet = !player.Equals("");
				if (playerSet) {
					if (Global.Network.Enable)
						status += "Monitoring " + player;
					else
						status += "Waiting for " + player + "...";
				} else {
					if (choice.Equals("<none>"))
						status += "Configure monitor";
					else
						status += "Waiting for any player...";
				}
			}
			return status;
		}
	}
	
	string MonitorMessage {
		get {
			string message = "";
			message += string.Format("<b>Monitor mode</b>\nSelect this mode if you want this device to monitor the activity of a player.\n");
			message += string.Format("Multiple monitors may be arranged around the player to display a greater field of view and create a more immersive environment\n\n");
			if (Global.Network.Enable)
				message += "Click <i>Monitor</i> until you reach the ID# of a player or enter this number directly in <i>Source player</i>.";
			else if (Global.Network.IDs.Count == 0)
				message += "This device is not connected to the network so it cannot be used as a monitor.";
			else if (Global.Network.IDs.Count == 1)
				message += "There are not players broadcasting right now. Enter the ID# of this device in the <i>Monitor list</i> of the player. Then click <i>Monitor</i> here until you reach the ID# of that device.";
			else if (Global.Network.IDs.Count > 1)
				message += "There are not players broadcasting right now. Enter one of the ID#s of this device in the <i>Monitor list</i> of the player. Then click <i>Monitor</i> here until you reach the ID# of that device.";
			return message;
		}
	}
	
	void OnHeaders(bool success, WebHeaderCollection headers) {
	}
	
	static public float Elapsed {
		get {
			return RunTime - timeOffset;
		}
		set {
			timeOffset = RunTime - value;
		}
	}
	
	static public string ElapsedString {
		get {
			return Elapsed.ToString("#0.0000");
		}
	}
	
	static public float RunTime {
		get {
			return 1e-3f*stopwatch.ElapsedMilliseconds;
		}
	}
}
	
class TextureData {
	public Material Target {get; set;}
	public bool Verbose {get; set;}
	
	public TextureData(Material material, bool verbose) {
		Target = material;
		Verbose = verbose;
	}
}

class BundleData {
	public string GroupId {set; get;}
	public Vector3 Scale {set; get;}
	public Vector3 Position {set; get;}
	public Vector3 Rotation {set; get;}
	public PickupData PickupData {get; set;}
	public bool Verbose {get; set;}
	
	public BundleData(string groupId, Vector3 scale, Vector3 position, Vector3 rotation, PickupData pickupData, bool verbose) {
		GroupId = groupId;
		Scale = scale;
		Position = position;
		Rotation = rotation;
		PickupData = pickupData;
		Verbose = verbose;
	}
}

class PickupData {
	public string Tag {set; get;}
	public int Pin {set; get;}
	public float Delay {set; get;}
	public float[] DurationRange {set; get;}
	public float[] Tone {set; get;}
	public float Interval {set; get;}
	public float Probability {set; get;}
	
	public PickupData() {
		Tone = new float[]{0, 0};
	}
	
	public PickupData(string tag, int pin, float delay, float[] durationRange, float[] tone, float interval, float probability) {
		Tag = tag;
		Pin = pin;
		DurationRange = durationRange;
		Tone = tone;
		Interval = interval;
		Probability = probability;
	}
}