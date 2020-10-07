/* 
 * 2015-09-19. Leonardo Molina.
 * 2019-08-05. Last modification.
 */

using System;

public interface IBridgeManager : IDisposable {
	event InputHandler InputChanged;
	event ConnectionHandler ConnectionChanged;
}