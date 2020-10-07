/* 
 * 2015-09-19. Leonardo Molina.
 * 2019-08-05. Last modification.
 */

using System.Collections.Generic;
using System.IO.Ports;
using UnityEngine;
using Timer = System.Timers.Timer;

public class WindowsBridgeManager : IBridgeManager {
	public event InputHandler InputChanged;
	public event ConnectionHandler ConnectionChanged;
	Dictionary<string, IBridge> workers = new Dictionary<string, IBridge>();
	int baudrate;
	double timeout;
	Timer ticker = new Timer(500d);
	
	public WindowsBridgeManager(int baudrate, double timeout) {
		this.baudrate = baudrate;
		this.timeout = timeout;
		ticker.Elapsed += OnTicker;
		ticker.AutoReset = true;
		ticker.Enabled = true;
		ticker.Start();
	}
	
	void OnTicker(object source, System.Timers.ElapsedEventArgs e) {
		// Open all available ports.
		List<string> names = new List<string>(SerialPort.GetPortNames());
		if (names.Contains("COM1"))
			names.Remove("COM1");
		foreach (string name in names) {
			if (!workers.ContainsKey(name)) {
				IBridge bridge = new WindowsBridge(name, baudrate, timeout);
				// Forward events.
				bridge.ConnectionChanged += OnConnectionChanged;
				bridge.InputChanged += OnInputChanged;
				workers[name] = bridge;
			}
		}
	}
	
	void OnConnectionChanged(IBridge bridge, bool connected) {
		ReportConnection(bridge, connected);
	}
	
	void OnInputChanged(IBridge bridge, byte[] input) {
		ReportInput(bridge, input);
	}
	
	void ReportConnection(IBridge bridge, bool connected) {
		if (ConnectionChanged != null)
			ConnectionChanged(bridge, connected);
	}
	
	void ReportInput(IBridge bridge, byte[] input) {
		if (InputChanged != null)
			InputChanged(bridge, input);
	}
	
	public void Dispose() {
		ticker.Enabled = false;
		ticker.Stop();
	}
}