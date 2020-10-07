/* 
 * 2015-09-19. Leonardo Molina.
 * 2019-08-05. Last modification.
 */

using System;

public delegate void InputHandler(IBridge bridge, byte[] input);
public delegate void ConnectionHandler(IBridge bridge, bool connected);

public interface IBridge : IDisposable {
	event InputHandler InputChanged;
	event ConnectionHandler ConnectionChanged;
	void Write(byte[] output);
}