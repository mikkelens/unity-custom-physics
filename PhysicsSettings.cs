using Tools.Types;
using UnityEngine;

namespace Physics
{
	[CreateAssetMenu(fileName = "New PhysicsSettings", menuName = "PhysicsSettings")]
	public class PhysicsSettings : ScriptableObject
	{
		[field: SerializeField] public float MinGroundNormalHeight { get; private set; } = 0.5f; // idk
		[field: SerializeField] public float MinimumMoveDistance { get; private set; } = 0.001f; // idk
		[field: SerializeField] public int MaxCollisionPreventionIterations { get; private set; } = 5; // idk
		[field: SerializeField] public int MaxRegisteredCollidersPerIteration { get; private set; } = 5; // idk
		[field: SerializeField] public Optional<LayerMask> CustomCollisionLayers { get; private set; } // defaulted during serialization
		[field: SerializeField] public Optional<float> Gravity { get; private set; } = 25f; // positive value, applied downwards
		[field: SerializeField] public Optional<float> MaxFallSpeed { get; private set; } = 10f; // used with fall speed
		[field: SerializeField] public Optional<float> CollisionFriction { get; private set; } = 0.25f;
		[field: SerializeField] public Optional<float> MinimumCollisionFriction { get; private set; } = 0.1f;
		[field: SerializeField] public int PushPriority { get; private set; }
	}
}