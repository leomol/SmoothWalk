/* 
 * 2016-08-17. Leonardo Molina.
 * 2017-04-06. Last modification.
*/

using UnityEngine;

class AndroidBridgeManager : IBridgeManager {
	public event InputHandler InputChanged;
	public event ConnectionHandler ConnectionChanged;
	
	GameObject bridgeGameObject;
	Component bridgeComponent;
	AndroidBridge bridge;
	
	public AndroidBridgeManager(int baudrate) {
		bridgeGameObject = new GameObject();
		bridgeGameObject.name = "AndroidBridge";
		bridgeComponent = bridgeGameObject.AddComponent<AndroidBridge>();
		bridge = (AndroidBridge) bridgeComponent;
		bridge.InputChanged += OnInputChanged;
		bridge.ConnectionChanged += OnConnectionChanged;
		bridge.Setup(baudrate);
	}
	
	void OnInputChanged(IBridge bridge, byte[] input) {
		if (InputChanged != null)
			InputChanged(bridge, input);
	}
	
	void OnConnectionChanged(IBridge bridge, bool connected) {
		if (ConnectionChanged != null)
			ConnectionChanged(bridge, connected);
	}
	
	public void Dispose() {
	}
}