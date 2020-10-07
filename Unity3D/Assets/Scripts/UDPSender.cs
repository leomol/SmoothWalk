/* UDP Sender.
 * 
 * 2014-05-19. Leonardo Molina.
 * 2017-09-22. Last modification.
 */
 
using System;
using System.Threading;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using UnityEngine;


class UDPSender : IDisposable {
	Queue<string> outputs = new Queue<string>();
	UdpClient socket;
	bool run = true;
	readonly object outputLock = new object();
	
	public UDPSender(string ip, int port) {
		socket = new UdpClient(ip, port);
		socket.Client.SendTimeout = 500;
		Thread loopThread = new Thread(new ThreadStart(Loop));
		loopThread.IsBackground = true;
		loopThread.Start();
	}
	
	public void Send(string text) {
		lock (outputLock)
			outputs.Enqueue(text);
	}
	
	void Loop() {
		while (run) {
			string output = null;
			lock (outputLock) {
				if (outputs.Count > 0)
					output = outputs.Dequeue();
			}
			if (output == null) {
				// Sleep when output is empty.
				Thread.Sleep(1);
			} else {
				byte[] bytes = Encoding.UTF8.GetBytes(output);
				try {
					// Make sure not to use a lock here because send is a synchronous operation with a long timeout.
					socket.Send(bytes, bytes.Length);
				} catch {
					Thread.Sleep(1);
				}
			}
		}
	}
	
	public void Dispose() {
		run = false;
	}
	
	public void Stop() {
		run = false;
	}
	
	~UDPSender() {
		run = false;
	}
}