using UnityEngine;

namespace Physics
{
	/// <summary>
	/// Common interface-like SO for adding movement things to physics items.
	/// </summary>
	public abstract class MovementScriptableObject : ScriptableObject
	{
		public abstract Vector2 UpdateVelocity(Vector2 newVelocity);
	}
}