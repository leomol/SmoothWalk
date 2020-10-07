/* 
 * 2015-02-05. Leonardo Molina.
 * 2017-04-03. Last modification.
 */
/*
	In Windows, errors thrown eventually by SerialPort NET 2.0 after calling ReadLine method:
		The I/O operation has been aborted because of either a thread exit or an application request
		The disk structure is corrupted and unreadable --> crash
		Cannot create a file when that file already exists --> crash
		Reached the end of the file --> crash
		Object reference not set to an instance of an object
		The system cannot find message text for message number 0x%1 in the message file for %2
		Logon failure: the specified account password has expired.
		The operation could not be completed. A retry should be performed.
 */
 
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
using UnityEngine;

using Timer = System.Timers.Timer;

public class WindowsBridge : IBridge {
	public event InputHandler InputChanged;
	public event ConnectionHandler ConnectionChanged;
	
	// Thread status.
	bool run = true;
	Queue<byte[]> outputs = new Queue<byte[]>();
	
	// Control.
	readonly object outputsLock = new object();
	bool once = true;
	public SerialPort port;
	Timer watchdog = new Timer();
	
	public WindowsBridge(string portName, int baudrate, double timeout) {
		// Assign variables locally.
		this.port = new SerialPort(portName, baudrate, Parity.None, 8, StopBits.One);
		this.port.ReadTimeout = 1;
		try {
			port.Open();
		} catch {}
		
		if (port.IsOpen) {
			// Let Arduino know that we are listening (C# SerialPort's default is false).
			port.DtrEnable = true;
			
			// Start timeout mechanism.
			watchdog.Elapsed += OnTimeOut;
			watchdog.Interval = 1e3d * timeout;
			watchdog.AutoReset = false;
			watchdog.Enabled = true;
			watchdog.Start();
			
			// Start thread.
			Thread thread = new Thread(new ThreadStart(Loop));
			thread.IsBackground = true;
			thread.Start();
		} else {
			ReportConnection(false);
		}
	}
	
	void ReportConnection(bool connected) {
		if (ConnectionChanged != null)
			ConnectionChanged(this, connected);
	}
	
	void ReportInput(byte[] input) {
		if (InputChanged != null)
			InputChanged(this, input);
	}
	
	void OnTimeOut(object source, System.Timers.ElapsedEventArgs e) {
		// Timer will execute even when disabled.
		run = false;
	}
	
	void DisableTimeout() {
		watchdog.Enabled = false;
		watchdog.Stop();
	}
	
	void Loop() {
		while (run) {
			bool inputAvailable = false;
			byte[] input = new byte[32];
			try {
				int n = port.Read(input, 0, 32);
				Array.Resize(ref input, n);
				inputAvailable = true;
			} catch (TimeoutException) {
				// Timeout is expected when no input is present.
			} catch {
				// If port closes unexpectedly (e.g. device is disconnected), stop thread.
				run = false;
			}
			
			if (inputAvailable) {
				if (once) {
					DisableTimeout();
					ReportConnection(true);
				}
				ReportInput(input);
			}
			
			
			// Write and remove output data.
			bool outputAvailable = false;
			byte[] output = null;
			while (run) {
				bool hasNext;
				bool hasCurrent;
				lock (outputsLock) {
					if (outputs.Count > 0) {
						outputAvailable = true;
						hasCurrent = true;
						hasNext = outputs.Count > 1;
						output = outputs.Dequeue();
					} else {
						hasCurrent = false;
						hasNext = false;
					}
				}
				if (hasCurrent)
					port.Write(output, 0, output.Length);
				if (!hasNext)
					break;
			}
			
			// Take a breath when both inputs and outputs are cleared.
			if (!inputAvailable && !outputAvailable)
				Thread.Sleep(1);
		}
		
		// Close and dispose port.
		try {
			port.Close();
			port.Dispose();
		} catch {}
		ReportConnection(false);
	}
	
	public void Write(byte[] output) {
		// Push data to output buffer.
		lock (outputsLock)
			outputs.Enqueue(output);
	}
	
	public void Dispose() {
		run = false;
	}
}