using System;
using Tools.Types;

namespace Physics
{
	[Serializable]
	public class CollisionStates
	{
		public TimedState grounded;
		public TimedState wallLeft;
		public TimedState wallRight;
		public TimedState ceilingAbove;

		public CollisionStates(TimedState startState = new TimedState())
		{
			grounded = startState;
			wallLeft = startState;
			wallRight = startState;
			ceilingAbove = startState;
		}

		public void UpdateStates(CollisionStates newStates)
		{
			grounded.State = newStates.grounded.State;
			wallLeft.State = newStates.wallLeft.State;
			wallRight.State = newStates.wallRight.State;
			ceilingAbove.State = newStates.ceilingAbove.State;
		}
	}
}