using Sirenix.OdinInspector;
using Tools.Types;
using UnityEngine;

namespace Physics
{
	/// <summary>
	/// Move settings for a hazard.
	/// </summary>
	[CreateAssetMenu(fileName = "New HazardMoveSettings", menuName = "HazardMoveSettings")]
	public class HazardMoveSettings : ScriptableObject
	{
		[field: SerializeField] public Optional<float> ContantHorizontalSpeed { get; private set; } = -2f;
		[InfoBox("This overrides gravity.", "@ConstantVerticalSpeed.Enabled")]
		[field: SerializeField] public Optional<float> ConstantVerticalSpeed { get; private set; } = -1f; // overrides gravity
	}
}