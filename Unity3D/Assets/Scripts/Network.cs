/* Bidirectional UDP communication.
 * 
 * 2014-10-04. Leonardo Molina.
 * Last modified: 2019-03-21.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;

public class Network : MonoBehaviour {
	// Server: Client targets.
	List<string> clients = new List<string>();
	// Player: Monitor targets.
	List<string> monitors = new List<string>();
	
	// Monitor: Invitations to join players as a monitor.
	List<string> players = new List<string>();
	
	// Monitor: Player source.
	string player = "";
	
	// Client: Object for sending data to client.
	Dictionary<string, UDPSender> senders = new Dictionary<string, UDPSender>();
	Dictionary<string, float> tics = new Dictionary<string, float>();
	
	Roles role = Roles.Client;
	
	// Handshake expected on every message.
	public string Handshake {get; set;}
	public int LocalPort {get; set;}
	public int Port {get; set;}
	string localhost = "127.0.0.1";
	
	// Communication variables.
	List<string> localIPs = new List<string>();
	UdpClient inSocket;
	volatile bool terminate = false;
	
	// Data received.
	List<string> data = new List<string>();
	
	readonly object localIPsLock = new object();
	readonly object dataLock = new object();
	readonly object monitorsLock = new object();
	readonly object clientsLock = new object();
	readonly object sendersLock = new object();
	readonly object playersLock = new object();
	
	void Awake() {
		// Register a local target at the selected port.
		senders[localhost] = new UDPSender(localhost, LocalPort);
		
		inSocket = new UdpClient(Port);
		inSocket.Client.ReceiveTimeout = 1000;
		Thread thread1 = new Thread(new ThreadStart(Check));
		thread1.IsBackground = true;
		thread1.Start();
		Thread thread2 = new Thread(new ThreadStart(Seconds));
		thread2.IsBackground = true;
		thread2.Start();
	}
	
	public string Player {
		get {
			return IP2ID(player);
		}
		set {
			player = ID2IP(value);
		}
	}
	
	public List<string> Players {
		get {
			return IPs2IDs(players);
		}
	}
	
	public List<string> Clients {
		get {
			lock (clientsLock)
				return IPs2IDs(clients);
		}
		set {
			List<string> ips = new List<string>();
			if (value.Count == 0 || IDs2IPs(value, out ips))
				lock (clientsLock)
					clients = ips;
		}
	}
	
	public List<string> Monitors {
		get {
			lock (monitorsLock)
				return IPs2IDs(monitors);
		}
		set {
			List<string> ips = new List<string>();
			if (value.Count == 0 || IDs2IPs(value, out ips))
				lock (monitorsLock)
					monitors = ips;
		}
	}
	
	/* Role of the device:\
		Player: Has monitors.
		Monitor: Has a player.
	*/
	public enum Roles {
		Client,
		Monitor
	}
	
	/* Target for broadcast messages:
		Clients: IDs listed in clients.
		Monitors: IDs listed in monitors.
		Player: For monitors, ID listed in player. For players, IDs listed in monitors.
	*/
	public enum Recipients {
		Clients,
		Monitors,
		Player
	}
	
	public Roles Role {
		get {
			return role;
		}
		set {
			// When role is changed, network data is cleared.
			if (!role.Equals(value)) {
				lock (dataLock)
					data.Clear();
				role = value;
			}
		}
	}
	
	public bool RoleClient {
		get {
			return Role == Roles.Client;
		}
	}
	
	public bool RolePlayer {
		get {
			return Role == Roles.Client;
		}
	}
	
	public bool RoleMonitor {
		get {
			return Role == Roles.Monitor;
		}
	}
	
	// Session ids derived from available IP addresses.
	public List<string> IDs {
		get {
			return IPs2IDs(IPs);
		}
	}
	
	// 000.000.000.000 --> 0000000000
	Dictionary<string, string> IP2IDList = new Dictionary<string, string>();
	public string IP2ID(string ip) {
		string id = "";
		if (IP2IDList.ContainsKey(ip)) {
			id = IP2IDList[ip];
		} else {
			string ipRegex = @"([0-9]{1,3})\.([0-9]{1,3})\.([0-9]{1,3})\.([0-9]{1,3})";
			ip = ip.Trim();
			IPAddress address;
			Match octets = Regex.Match(ip, @"^" + ipRegex + @"$");
			if (octets.Success && IPAddress.TryParse(ip, out address)) {
				long number = int.Parse(octets.Groups[4].Value) + 256L*int.Parse(octets.Groups[3].Value) + 65536L*int.Parse(octets.Groups[2].Value) + 16777216L*int.Parse(octets.Groups[1].Value);
				id = number.ToString("0000000000");
			}
		}
		IP2IDList[ip] = id;
		return id;
	}
	
	// 0000000000 --> 000.000.000.000
	Dictionary<string, string> ID2IPList = new Dictionary<string, string>();
	public string ID2IP(string id) {
		string ip = "";
		
		if (ID2IPList.ContainsKey(id)) {
			ip = ID2IPList[id];
		} else {
			id = id.Trim();
			List<string> octets = new List<string>();
			long number = 0;
			bool success = id.Length == 10 && long.TryParse(id, out number) && number >= 0;
			if (success) {
				long c = 16777216L;
				for (int j = 0; j < 4; j++) {
					long k = number / c;
					number -= c * k;
					c /= 256L;
					octets.Add(string.Format("{0}", k));
					success = success && k < 256L;
					success = success && k < 256L;
				}
			}
			IPAddress address;
			string test = string.Join(".", octets.ToArray());
			success &= IPAddress.TryParse(test, out address);
			if (success)
				ip = test;
		}
		ID2IPList[id] = ip;
		return ip;
	}
	
	bool IDs2IPs(List<string> ids, out List<string> ips) {
		bool success = true;
		ips = new List<string>();
		// No duplicates.
		success = (ids.Distinct().ToList()).Count == ids.Count;
		// Available interfaces.
		List<string> ips2 = IPs;
		if (success) {
			foreach (string id in ids) {
				// Not empty.
				success &= id.Length > 0;
				// Valid IP conversion.
				string ip = ID2IP(id);
				success &= ip.Length > 0;
				// Not own's ip address.
				bool remote = true;
				foreach (string ip2 in ips2)
					remote &= !ip.Equals(ip2);
				success &= remote;
				// Add empty if not remote or already empty.
				ips.Add(remote ? ip : "");
			}
		}
		return success;
	}
	
	List<string> IDs2IPs(List<string> ids) {
		List<string> ips = new List<string>();
		IDs2IPs(ids, out ips);
		return ips;
	}
	
	List<string> IPs2IDs(List<string> ips) {
		List<string> ids = new List<string>();
		foreach (string ip in ips)
			ids.Add(IP2ID(ip));
		return ids;
	}
	
	public bool Enable {
		get {
			bool e = true;
			switch (role) {
				case Roles.Client:
					break;
				case Roles.Monitor:
					lock (playersLock)
						e = players.Contains(player);
					break;
			}
			return e;
		}
	}
	
	// Check that ID is valid.
	bool Validate(List<string> ids) {
		List<string> ips = new List<string>();
		return IDs2IPs(ids, out ips);
	}
	
	// Check that ID is valid and different from self.
	public bool Validate(string recipients) {
		recipients = recipients.Trim();
		if (recipients.Length > 0)
			return Validate(new List<string>(Regex.Split(recipients.Trim(), @"[\s,;]+")));
		else
			return true;
	}

	public List<string> IPs {
		get {
			lock (localIPsLock)
				return new List<string>(localIPs);
		}
	}
	
	public void Send(Recipients recipients, string message) {
		Send(recipients, "default", message);
	}
	
	/* Source.
		Source="". Empty is used for communication between players and monitors, mostly for global parameters.
		Source="". Empty is also used for invitations. Other commands are ignored if device isn't listed.
		
		Source=IP. IP has two purposes:
			1) Excludes IP from the recipients list when forwarding data.
			2) Identifies the source of the forwarded message at the receiver.
		
		Source=Player. Player has two behaviors:
			Player to monitor: Instruction targeting the local player.
			Monitor to player: Idem.
	*/
	public void Send(Recipients recipients, string source, string message) {
		List<string> ips = new List<string>();
		string recipientRole = "";
		bool success = true;
		switch (recipients) {
			case Recipients.Clients:
				lock (clientsLock)
					ips = new List<string>(clients);
				recipientRole = "client";
				break;
			case Recipients.Monitors:
				lock (monitorsLock)
					ips = new List<string>(monitors);
				recipientRole = "monitor";
				break;
			case Recipients.Player:
				ips = new List<string>(){player};
				recipientRole = "player";
				break;
			default:
				success = false;
				break;
		}
		// Add local host; sender has been created with a different target port.
		ips.Add(localhost);
		
		success &= ips.Count > 0 && ips[0].Length > 0;
		if (success) {
			// Remove spaces and ';' at the end of the command.
			// UnityEngine.Debug.Log(message);
			
			message = message.TrimEnd(new Char[]{' ', ';'});
			message = Handshake + "," + Role.ToString().ToLower() + "," + recipientRole + "," + source + ";" + message + ";";
			foreach (string ip in ips) {
				if (ip.Equals(source))
					continue;
				
				// Create senders if they don't exist.
				lock (sendersLock) {
					if (!senders.ContainsKey(ip))
						senders[ip] = new UDPSender(ip, Port);
					senders[ip].Send(message);
				}
			}
		}
	}
	
	void OnApplicationQuit() {
		// Stop senders.
		lock (sendersLock) {
			foreach (UDPSender sender in senders.Values.ToArray())
				sender.Stop();
		}
		// Stop all incoming messages.
		terminate = true;
		inSocket.Close();
	}
	
	bool TestRole(string role) {
		bool success = false;
		switch (role) {
			case "client":
				success = RoleClient;
				break;
			case "player":
				success = RolePlayer;
				break;
			case "monitor":
				success = RoleMonitor;
				break;
			default:
				success = false;
				break;
		}
		return success;
	}
	
	bool TestSender(string senderRole, string senderIp) {
		bool success = false;
		bool isLocal = senderIp.Equals("127.0.0.1");
		switch (senderRole) {
			case "player":
			case "client":
				success |= RoleMonitor && player.Equals(senderIp);
				success |= RoleMonitor && isLocal;
				break;
			case "monitor":
				lock (monitorsLock)
					success |= RolePlayer && monitors.Contains(senderIp);
				success |= RolePlayer && isLocal;
				break;
		}
		return success;
	}

	void Check() {
		int breath_count = 0;
		while (!terminate) {
			// Well formatted instruction: handshake, sender role, recipient role, id of source; command1, parameters1; command2, parameters2; ...;
			bool success = true;
			string senderMessage = "";
			IPEndPoint anyEP = new IPEndPoint(IPAddress.Any, Port);
			try {
				senderMessage = Encoding.UTF8.GetString(inSocket.Receive(ref anyEP));
				if (++breath_count % 100 == 0)
					Thread.Sleep(1);
				// UnityEngine.Debug.Log(senderMessage);
			} catch {
				success = false;
				Thread.Sleep(1);
			}
			if (success) {
				string senderIp = anyEP.Address.ToString();
				string senderHandshake = "";
				string recipientRole = "";
				string senderRole = "";
				string source = "";
				string header = "";
				Tools.Collect(ref senderMessage, ref header);
				senderMessage = senderMessage.Trim();
				// If header makes sense.
				if (Tools.Parse(ref header, ref senderHandshake)) {
					senderHandshake = senderHandshake.Trim();
					if (senderHandshake.Equals(Handshake)) {
						success &= Tools.Parse(ref header, ref senderRole);
						success &= Tools.Parse(ref header, ref recipientRole);
						success &= Tools.Parse(ref header, ref source);
						recipientRole = recipientRole.Trim();
						senderRole = senderRole.Trim();
						source = source.Trim();
						if (success && TestRole(recipientRole)) {
							// Extend expiry date for that sender.
							tics[senderIp] = Control.Elapsed;
							if (senderMessage.Equals("monitor,1;")) {
								lock (playersLock) {
									if (!players.Contains(senderIp))
										players.Add(senderIp);
								}
							} else if (TestSender(senderRole, senderIp)) {
								// Change empty source to IP unless communication is Player <--> monitor.
								bool keep = RoleMonitor || (RolePlayer && senderRole.Equals("monitor"));
								if (source.Equals("default") && !keep)
									source = senderIp;
								Push(source + "," + senderMessage);
							}
						}
					}
				}
			}
		}
	}
	
	void Seconds() {
		while (!terminate) {
			bool available = false;
			
			// Update host in a thread.
			IPHostEntry host = new IPHostEntry();
			try {
				host = Dns.GetHostEntry(Dns.GetHostName());
				available = true;
			} catch {
				available = false;
				lock (localIPsLock)
					localIPs.Clear();
			}
			
			if (available) {
				// As long as monitors are defined, players notify them about their availability.
				if (RolePlayer)
					Send(Recipients.Monitors, "monitor,1; ");
				
				lock (localIPsLock) {
					localIPs.Clear();
					IPAddress[] addressList = host.AddressList;
					foreach (IPAddress address in addressList) {
						string ip = address.ToString();
						if (address.AddressFamily == AddressFamily.InterNetwork)
							localIPs.Add(ip);
					}
				}
				
				lock (playersLock) {
					List<string> players2 = new List<string>(players);
					foreach (string ip in players)
						if (!tics.ContainsKey(ip) || Control.Elapsed > tics[ip] + 1f)
							players2.Remove(ip);
					players = players2;
				}
			}
			
			Thread.Sleep(1000);
		}
	}
	
	void Push(string message) {
		lock (dataLock) {
			// Add new message.
			data.Add(message);
			// Don't let data build up indefenitely.
			data.RemoveRange(0, Mathf.Max(data.Count - 500,0));
		}
	}
	
	public string Data {
		get {
			string next = "";
			lock (dataLock) {
				if (data.Count > 0) {
					next = data.ElementAt(0);
					data.RemoveAt(0);
				}
			}
			return next;
		}
	}
}