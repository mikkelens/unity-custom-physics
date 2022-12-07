using Character;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Physics
{
	public class PhysicsBoxHazard : PhysicsBox
	{
		[Header("Hazard Settings")]
		[SerializeField, Required] private HazardMoveSettings move;

		private protected override Vector2 UpdateVelocity(Vector2 newVelocity)
		{
			if (move.ContantHorizontalSpeed.Enabled)
			{
				newVelocity.x = move.ContantHorizontalSpeed.Value;
			}
			if (move.ConstantVerticalSpeed.Enabled)
			{
				newVelocity.y = move.ConstantVerticalSpeed.Value;
			}
			return newVelocity;
		}

		private protected override Vector2 HandleCollision(Vector2 newStep, RaycastHit2D hit)
		{
			Player player = hit.transform.GetComponent<Player>();
			if (player != null)
			{
				// hurt player, do not change velocity
				player.Hit();
				return newStep;
			}

			return base.HandleCollision(newStep, hit);
		}
	}
}