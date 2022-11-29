using UnityEngine;

namespace Physics
{
	public class MovingBox : PhysicsBox
	{
		[SerializeField] private Vector2 moveVelocities = new Vector2(0f, 1f);

		private protected override Vector2 UpdateVelocity(Vector2 newVelocity)
		{
			return moveVelocities;
		}
	}
}