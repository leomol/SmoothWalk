/* Pickup.
 * 2015-06-17. Leonardo Molina.
 * 2016-07-27. Last modification.
 */

using System;
using System.Timers;
using UnityEngine;

public class Pickup : MonoBehaviour {
	public Collider Sensor;
	public string Tag = "";
	public int Pin = 0;
	public float Probability = 1f;
	public float Delay = 0f;
	public float[] DurationRange = new float[]{0f, 0f};
	public float[] Tone = new float[]{0f, 0f};
	bool invoked = false;
	bool enable = true;
	bool armed = true;
	float interval = 1f; //intermission, recess, dormancy, interval, gap.
	float waitStart = 0f;
	System.Timers.Timer intervalTicker = new System.Timers.Timer();
	
	public Pickup() {
	}
	
	public float Duration {
		get {
			return UnityEngine.Random.Range(DurationRange[0], DurationRange[1]);
		}
		set {
			DurationRange[0] = value;
			DurationRange[1] = value;
		}
	}
	
	public bool Enable {
		get {
			return enable;
		}
	}

	public Action<Pickup, Events, float> Callback;
	
	public float Interval {
		set {
			interval = value;
			StopRearm();
			if (interval > 0) {
				intervalTicker.Interval = 1000f*value;
				StartRearm();
			} else if (interval < 1e-3){
				Rearm();
			}
		}
		get {
			return interval;
		}
	}
	
	public enum Events {
		Enter,
		Trigger,
		Exit,
		Premature,
	}
	
	public void Rearm() {
		armed = true;
	}
	
	public string Layer {
		set {
			int id = LayerMask.NameToLayer(value);
			if (id > 0 && value.Length > 0)
				gameObject.layer = id;
		}
	}
	
	void Awake() {
		intervalTicker.Elapsed += OnInterval;
		intervalTicker.AutoReset = false;
	}
	
	void OnInterval(object source, ElapsedEventArgs e) {
		// Rearm only if interval continues to be positive.
		if (interval >= 0f)
			armed = true;
	}
	
	void OnTriggerEnter(Collider block) {
		if (armed) {
			invoked = false;
			armed = false;
			enable = UnityEngine.Random.Range(0f, 1f) <= Probability;
		}
		
		// On every entry: Report and reset delay timer.
		waitStart = Control.Elapsed;
		
		// Callback after defining luck.
		Invoke(Events.Enter, 0f);
		
		// Avoid delays by checking before and during stay.
		CheckWait();
		
		// Disable rearming until exit.
		StopRearm();
	}
	
	void OnTriggerStay(Collider block) {
		CheckWait();
	}
	
	void CheckWait() {
		if ((Wait >= Delay || Delay < 1e-3f) && enable && !invoked) {
			Invoke(Events.Trigger, Wait);
			invoked = true;
		}
	}
	
	void OnTriggerExit(Collider block) {
		if (Wait < Delay && Delay > 0f) {
			// Exited too soon.
			Invoke(Events.Premature, Wait);
		} else {
			// Exited on time.
			CheckWait();
			Invoke(Events.Exit, Wait);
		}
		StartRearm();
	}
	
	void StartRearm() {
		// Initate rearming mechanism.
		intervalTicker.Enabled = true;
		intervalTicker.Start();
	}
	
	void StopRearm() {
		intervalTicker.Enabled = false;
		intervalTicker.Stop();
	}
	
	float Wait {
		get {
			return Control.Elapsed - waitStart;
		}
	}
	
	void Invoke(Events events, float elapsed) {
		if (Callback != null)
			Callback(this, events, elapsed);
	}
}