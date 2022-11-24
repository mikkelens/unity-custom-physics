using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Scripts.Tools.Types;
using Sirenix.OdinInspector;
using Tools.Helpers;
using UnityEngine;

namespace Scripts.Physics
{
	/// <summary>
	/// Simple physics subclass for entities or players.
	/// It does not interact with rigidbodies, but it does not need to.
	/// All it needs is a box to describe its size.
	///
	/// For effective use, use 100 ticks/s FixedUpdate rate (0.01 fixedDeltaTime),
	/// and simulation mode "Fixed Update".
	///
	/// To update velocity, use "UpdateVelocity()" method
	/// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    [SuppressMessage("ReSharper", "Unity.InefficientPropertyAccess")] // I don't care about transform.position warnings
	public class PhysicalBox : MonoBehaviour
	{
		[Header("Physics Settings")]
		[SerializeField] private PhysicsSettings physics; // see below

	    [Header("Physics Debug")]
	    [SerializeField] private Optional<Vector2> startVelocity;
	    [SerializeField] private Optional<float> debugFixedDeltaTime = new Optional<float>(0.25f); // delay between physics steps, default is 4 updates per second
	    [SerializeField] private Optional<float> debugTimeScale = new Optional<float>(0.5f); // slow down time, default is half speed
	    [SerializeField, ReadOnly] private Vector2 velocity;
	    [SerializeField, ReadOnly] private TimedState grounded;
	    [SerializeField, ReadOnly] private TimedState touchingWallLeft;
	    [SerializeField, ReadOnly] private TimedState touchingWallRight;
	    [SerializeField, ReadOnly] private TimedState touchingCeiling;

	    private protected TimedState Grounded => grounded;
	    private protected TimedState TouchingWallLeft => touchingWallLeft;
	    private protected TimedState TouchingWallRight => touchingWallRight;
	    private protected TimedState TouchingCeiling => touchingCeiling;

	    private BoxCollider2D _box;
	    private BoxCollider2D Box => _box != null ? _box : _box = GetComponent<BoxCollider2D>();
	    private Vector2 TrueSize => Vector2.Scale(Box.size, transform.localScale.AsV2());

	    // CollisionLayers should possibly be inverted with an "~" prefix
	    private LayerMask CollisionLayers => physics.CustomCollisionLayers.Enabled
		    ? physics.CustomCollisionLayers
		    : LayerMask.GetMask("Default");

	    private void Awake()
	    {
		    if (startVelocity.Enabled) velocity = startVelocity.Value;
	    }

	    private void FixedUpdate()
	    {
		    // debug
		    if (debugFixedDeltaTime.Enabled) Time.fixedDeltaTime = debugFixedDeltaTime.Value; // may not work...
		    if (debugTimeScale.Enabled) Time.timeScale = debugTimeScale.Value;

		    // physics sandbox settings
		    if (physics.Gravity.Enabled) velocity.y -= physics.Gravity.Value * Time.fixedDeltaTime;

		    UpdateVelocity(ref velocity);

		    // Velocity fix as collision handling. think this is called continuous interpolated physics?
		    Vector2 step = velocity * Time.fixedDeltaTime;

		    step = CorrectStep(step);

		    // apply physics movement
		    if (step.magnitude >= physics.MinimumMoveDistance)
		    {
			    transform.Translate(step);

			    Vector3 latePos = transform.position;
				Debug.DrawLine(latePos, latePos + velocity.AsV3() * Time.fixedDeltaTime, Color.red, Time.fixedDeltaTime); // prediction line
		    }

		    // remember velocity from step
		    velocity = step / Time.fixedDeltaTime;
	    }

	    private protected virtual void UpdateVelocity(ref Vector2 newVelocity) {}

	    private Vector2 CorrectStep(Vector2 step)
	    {
		    // reset collision dependent states
		    bool ground = false;
		    bool ceiling = false;
		    bool wallLeft = false;
		    bool wallRight = false;

		    // make sure we only deal with the same collider once per physics step
		    List<Collider2D> collidersToIgnore = new List<Collider2D>();
		    for (int i = 0; i < physics.MaxCollisionPreventionIterations; i++) // each iteration
		    {
			    RaycastHit2D[] hitArray = new RaycastHit2D[physics.MaxRegisteredCollidersPerIteration];
			    int hitCount = Physics2D.BoxCastNonAlloc(transform.position.AsV2(), TrueSize, 0f, step.normalized, hitArray, step.magnitude, CollisionLayers.value);

			    if (hitCount == 0) break;

			    for (int h = 0; h < hitCount; h++)
			    {
					RaycastHit2D hit = hitArray[h];
					if (collidersToIgnore.Contains(hit.collider) || hit.collider == _box) continue; // hit a previous collider, skip resolve test

					Vector2 hitNormal = hit.normal;
					// Debug.Log($"Hit collider: {hit.collider}, Hit normal: {hitNormal.ToString()}");

					// remember touches
					if (hitNormal.y >= physics.MinGroundNormalHeight) ground = true;
					if (hitNormal.x > 0) wallLeft = true;
					if (hitNormal.x < 0) wallRight = true;
					if (hitNormal.y <= -physics.MinGroundNormalHeight) ceiling = true;

					Vector2 travelDirection = step.normalized;
					float cancelRelevancy = Vector2.Dot(-hitNormal, travelDirection);
					if (cancelRelevancy < 0f) continue; // dont apply corrections when not going towards collider

					// find solve correction
					float cancelVelocity = (1 - hit.fraction) * step.magnitude;
					Vector2 correction = cancelVelocity * cancelRelevancy * hit.normal;
					Debug.DrawRay(hit.point, correction, Color.green, Time.fixedDeltaTime); // show correction for 1 frame
					// Debug.Log($"Applied correction: {correction.ToString()}");

					if (physics.Friction.Enabled)
					{
						// friction applied based on impact harshness
						Vector2 rightAxis = Vector2.Perpendicular(hit.normal);
						float stepAxisLikeness = Vector2.Dot(rightAxis, -step.normalized);
						float maxFriction = step.magnitude * Mathf.Abs(stepAxisLikeness);
						float frictionStrength = maxFriction * physics.Friction.Value;
						if (physics.MinimumFriction.Enabled) frictionStrength = Mathf.Max(physics.MinimumFriction, frictionStrength);
						float frictionAmount = Mathf.MoveTowards(0f, maxFriction, frictionStrength * Time.fixedDeltaTime);
						Vector2 frictionDirection = rightAxis * stepAxisLikeness.AsSignedInt();
						Vector2 friction = frictionAmount * frictionDirection;
						correction += friction;
					}

					step += correction; // apply correction
					collidersToIgnore.Add(hit.collider);
			    }
		    }
		    // update states
		    grounded.State = ground;
		    touchingWallLeft.State = wallLeft;
		    touchingWallRight.State = wallRight;
		    touchingCeiling.State = ceiling;

		    return step;
	    }

	    private Vector2 GetSmallestVectorToObstacle(RaycastHit2D hit)
	    {
		    Vector2 distancesFromCenter = hit.point - transform.position.AsV2();
		    Vector2 offset = Vector2.Min(TrueSize, distancesFromCenter);
		    return distancesFromCenter - offset;
	    }

	    private protected float HeightToUpwardsVelocity(float height) => physics.Gravity.Enabled ? Mathf.Sqrt(2 * height * physics.Gravity) : height;

		private void OnDrawGizmosSelected()
	    {
		    // last physical position
		    Vector2 pos = transform.position.AsV2();
		    Gizmos.color = Color.green;
		    Gizmos.DrawWireCube(pos, TrueSize);

		    Vector2 posNextFrame = pos + velocity * Time.fixedDeltaTime;
		    Gizmos.color = Color.blue;
		    Gizmos.DrawWireCube(posNextFrame, TrueSize);
	    }
    }
}