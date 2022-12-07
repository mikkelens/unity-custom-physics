using UnityEngine;

namespace Physics
{
	// todo: use this instead of hierachical inheritance
	public sealed class PlayerGravityObject : GravityObject
	{
		[SerializeField] private float fallGravityModifier = 2f;

		private protected override float Gravity => base.Gravity;
	}
}