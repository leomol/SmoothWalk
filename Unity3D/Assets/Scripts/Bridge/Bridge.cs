/* 
 * 2015-09-19. Leonardo Molina.
 * 2016-05-16. Last modification.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class Bridge : IDisposable {
	public delegate void ConnectionHandler(Bridge bridge, bool connection);
	public delegate void InputHandler(Bridge bridge, int pin, int state);
	
	public event ConnectionHandler ConnectionChanged;
	public event InputHandler InputChanged;
	
	readonly object eventLock = new object();
	bool connected = false;
	bool quitting = false;
	const int MAX_PIN = 126;
	int[] states = new int[128];
	bool[] setup = new bool[128];
	ulong[,] counts = new ulong[128, 2];
	int[] factor = new int[128];
	
	IBridge bridge;
	Dictionary<IBridge, string> buffer = new Dictionary<IBridge, string>();
	IBridgeManager manager;
	string greeting = "protocol:\"r or d?\"\n" + Encoding.ASCII.GetString(new byte[]{255});
	int baudrate;
	
	public Bridge(int baudrate) {
		// Initialize states.
		for (int pin = 0; pin < 128; pin++)
			ResetPin(pin);
		this.baudrate = baudrate;
		NewManager();
	}
	
	public void Dispose() {
		lock (eventLock) {
			quitting = true;
			manager.Dispose();
			if (bridge != null)
				bridge.Dispose();
		}
	}
	
	void NewManager() {
		#if UNITY_ANDROID && !UNITY_EDITOR
		manager = new AndroidBridgeManager(this.baudrate);
		#else
		manager = new WindowsBridgeManager(this.baudrate, 3d);
		#endif
		manager.InputChanged += OnInputChanged;
		manager.ConnectionChanged += OnConnectionChanged;
	}
	
	void OnInputChanged(IBridge bridge, byte[] input) {
		lock (eventLock) {
			if (quitting) {
				bridge.Dispose();
			} else if (connected) {
				if (this.bridge == bridge) {
					int pin;
					int state;
					foreach (byte b in input) {
						DecodeState(b, out pin, out state);
						states[pin] = state;
						// First message indicates state, not change.
						if (setup[pin]) {
							counts[pin, state] += (ulong) factor[pin];
							ReportInput(pin, state);
						} else {
							setup[pin] = true;
						}
					}
				} else {
					// Another worker had previously hanshook.
					bridge.Dispose();
				}
			} else {
				// Verify signature.
				if (!buffer.ContainsKey(bridge))
					buffer[bridge] = "";
				buffer[bridge] += Encoding.ASCII.GetString(input);
				if (buffer[bridge].IndexOf(greeting) >= 0) {
					// 2) Expect 255 back.
					this.bridge = bridge;
					connected = true;
					manager.Dispose();
					// Clear buffer.
					buffer.Clear();
					ReportConnection(true);
				} else if (buffer[bridge].IndexOf("protocol:\"r or d?\"\n") >= 0) {
					// 1) Receive protocol:"r or d?" and respond with r.
					bridge.Write(new byte[]{(byte) 'r'});
				} else if (greeting.IndexOf(buffer[bridge]) == -1) {
					// Not a match.
					bridge.Dispose();
				}
			}
		}
	}
	
	void OnConnectionChanged(IBridge bridge, bool connected) {
		lock (eventLock) {
			if (!connected && bridge == this.bridge) {
				// Previous connection is no longer available.
				bridge.Dispose();
				this.connected = false;
				ReportConnection(false);
				// Scan.
				NewManager();
			} else if (connected && this.connected && bridge != this.bridge) {
				// Another connection was already made.
				bridge.Dispose();
			}
		}
	}
	
	void ReportConnection(bool connected) {
		if (ConnectionChanged != null)
			ConnectionChanged(this, connected);
	}
	
	void ReportInput(int pin, int state) {
		if (InputChanged != null)
			InputChanged(this, pin, state);
	}
	
	int EncodeState(int pin, int state) {
		if (state == 1)
			pin = pin + MAX_PIN + 1;
		return pin;
	}
	
	void DecodeState(int code, out int pin, out int state) {
		if (code <= MAX_PIN) {
			pin = code;
			state = 0;
		} else {
			pin = code - MAX_PIN - 1;
			state = 1;
		}
	}
	
	byte[] Compress(int[] numbers, int[] sizes) {
		int id = 0;
		int cum = 0;
		int sum = 0;
		foreach (int s in sizes)
			sum += s;
		byte[] bytes = new byte[Mathf.CeilToInt(sum / 8f)];
		for (int k = 0; k < numbers.Length; k++) {
			cum += sizes[k];
			int l = Mathf.CeilToInt(cum / 8f);
			int shift = 8 * l - cum;
			int number = numbers[k] << shift;
			for (int b = id; b < l; b++)
				bytes[b] |= GetByte(number, l - b - 1);
			id = l - 1;
			if (cum % 8 == 0)
				id += 1;
		}
		return bytes;
	}
	
	byte GetByte(int number, int p) {
		return (byte) (255 & (number >> 8*p));
	}
	
	void ResetPin(int pin) {
		setup[pin] = false;
		states[pin] = -1;
		counts[pin, 0] = 0;
		counts[pin, 1] = 0;
	}
	
	public long GetValue(int pin) {
		if (counts[pin, 1] >= counts[pin, 0])
			return +(long) (counts[pin, 1] - counts[pin, 0]);
		else
			return -(long) (counts[pin, 0] - counts[pin, 1]);
	}
	
	public ulong GetCount(int pin, int state) {
		return counts[pin, state];
	}
	
	public void Write(byte[] output) {
		lock (eventLock) {
			if (connected)
				bridge.Write(output);
		}
	}
	
	public void SetBinary(int pin, int state) {
		Write(new byte[]{(byte) EncodeState(pin, state)});
	}
	
	public void SetAddress(int address, int val) {
		Write(new byte[]{254, (byte) address, (byte) val});
	}
	
	public void StopSet(int pin) {
		int[] numbers = new int[]{4095, 0, pin};
		int[] sizes = new int[]{12, 1, 7};
		Write(Compress(numbers, sizes));
	}
	
	public void StopGet(int pin) {
		int[] numbers = new int[]{4095, 1, pin};
		int[] sizes = new int[]{12, 1, 7};
		Write(Compress(numbers, sizes));
	}
	
	public void SetPulse(int pin, int stateStart, int durationLow, int durationHigh, int repetitions) {
		int[] numbers = new int[]{4080, pin, stateStart, durationLow, durationHigh, repetitions};
		int[] sizes = new int[]{12, 7, 1, 24, 24, 24};
		Write(Compress(numbers, sizes));
	}
	
	public void SetChirp(int pin, int durationLowStart, int durationLowEnd, int durationHighStart, int durationHighEnd, int duration) {
		int[] numbers = new int[]{4081, pin, durationLowStart, durationLowEnd, durationHighStart, durationHighEnd, duration};
		int[] sizes = new int[]{12, 7, 24, 24, 24, 24, 24};
		Write(Compress(numbers, sizes));
	}
	
	public void GetBinary(int pin, int debounceRising, int debounceFalling, int factor) {
		ResetPin(pin);
		this.factor[pin] = factor;
		int[] numbers = new int[]{4088, pin, debounceRising, debounceFalling, factor};
		int[] sizes = new int[]{12, 7, 24, 24, 8};
		Write(Compress(numbers, sizes));
	}
	
	public void GetLevel(int pin, int debounceRising, int debounceFalling) {
		ResetPin(pin);
		this.factor[pin] = 1;
		int[] numbers = new int[]{4089, pin, debounceRising, debounceFalling};
		int[] sizes = new int[]{12, 7, 24, 24};
		Write(Compress(numbers, sizes));
	}
	
	public void GetRotation(int pin1, int pin2, int factor) {
		ResetPin(pin1);
		ResetPin(pin2);
		this.factor[pin1] = factor;
		int[] numbers = new int[]{4090, pin1, pin2, factor};
		int[] sizes = new int[]{12, 7, 7, 8};
		Write(Compress(numbers, sizes));
	}
}