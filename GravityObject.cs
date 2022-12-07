using Tools.Types;
using UnityEngine;

namespace Physics
{
	[CreateAssetMenu(fileName = "New GravityObject", menuName = "GravityObject")]
	public class GravityObject : MovementScriptableObject
	{
		[SerializeField] private protected float baseGravity = 10f;
		[SerializeField] private Optional<float> maxFallSpeed = 14f;

		private protected virtual float Gravity => baseGravity;

		public override Vector2 UpdateVelocity(Vector2 newVelocity)
		{
			newVelocity.y -= Gravity * Time.fixedDeltaTime;
			if (maxFallSpeed.Enabled) newVelocity.y = Mathf.Max(newVelocity.y, -maxFallSpeed.Value);
			return newVelocity;
		}
	}
}