using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Sirenix.OdinInspector;
using Tools.Helpers;
using Tools.Types;
using UnityEngine;

namespace Physics
{
	/// <summary>
	/// Simple physics subclass for entities or players.
	/// It does not interact with rigidbodies, but it does not need to.
	/// All it needs is a box to describe its size.
	///
	/// For effective use, use 100 ticks/s FixedUpdate rate (0.01 fixedDeltaTime),
	/// and simulation mode "Fixed Update".
	///
	/// To update velocity, use "UpdateVelocity()" method.
	///
	/// Collisions between physics boxes will be with priority, or using simple vector solving.
	/// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    [SuppressMessage("ReSharper", "Unity.InefficientPropertyAccess")] // I don't care about transform.position warnings
	public class PhysicsBox : MonoBehaviour
	{
		[Header("Physics Settings")]
		[SerializeField] private PhysicsSettings physics; // see below

		private PhysicsSettings P => physics;

	    [Header("Physics Debug")]
	    [SerializeField] private Optional<Vector2> startVelocity;
	    [SerializeField] private Optional<float> debugFixedDeltaTime = new Optional<float>(0.25f); // delay between physics steps, default is 4 updates per second
	    [SerializeField] private Optional<float> debugTimeScale = new Optional<float>(0.5f); // slow down time, default is half speed
	    [SerializeField, ReadOnly] private Vector2 velocity;

	    [field: SerializeField, ReadOnly]
	    private protected CollisionStates CollisionStates { get; private set; }

	    private BoxCollider2D _box;
	    private BoxCollider2D Box => _box != null ? _box : _box = GetComponent<BoxCollider2D>();
	    private Vector2 TrueSize => Vector2.Scale(Box.size, transform.localScale.AsV2());

	    // CollisionLayers should possibly be inverted with an "~" prefix
	    private LayerMask CollisionLayers => P.CustomCollisionLayers.Enabled
		    ? P.CustomCollisionLayers
		    : LayerMask.GetMask("Default");

	    private protected virtual void Awake()
	    {
		    if (startVelocity.Enabled) velocity = startVelocity.Value;
	    }

	    private protected virtual void Start()
	    {
		    CollisionStates = new CollisionStates();
	    }

	    private void FixedUpdate()
	    {
		    // debug
		    if (debugFixedDeltaTime.Enabled) Time.fixedDeltaTime = debugFixedDeltaTime.Value; // may not work...
		    if (debugTimeScale.Enabled) Time.timeScale = debugTimeScale.Value;

		    // physics sandbox settings
		    if (P.Gravity.Enabled) velocity.y -= P.Gravity.Value * Time.fixedDeltaTime;

		    velocity = UpdateVelocity(velocity);

		    // Velocity fix as collision handling. think this is called continuous interpolated physics?
		    Vector2 step = velocity * Time.fixedDeltaTime;

		    step = CorrectStep(step);

		    // apply physics movement
		    // if (step.magnitude >= P.MinimumMoveDistance)
		    {
			    transform.Translate(step);

			    // Vector3 latePos = transform.position;
				// Debug.DrawLine(latePos, latePos + velocity.AsV3() * Time.fixedDeltaTime, Color.red, Time.fixedDeltaTime); // prediction line
		    }

		    // remember velocity from step
		    velocity = step / Time.fixedDeltaTime;
	    }

	    private protected virtual Vector2 UpdateVelocity(Vector2 newVelocity) => newVelocity;

	    private Vector2 CorrectStep(Vector2 step)
	    {
		    // reset collision dependent states
		    CollisionStates newStates = new CollisionStates(); // false by default

		    // make sure we only deal with the same collider once per physics step
		    List<Collider2D> collidersToIgnore = new List<Collider2D>();
		    for (int i = 0; i < P.MaxCollisionPreventionIterations; i++) // each iteration
		    {
			    RaycastHit2D[] hitArray = new RaycastHit2D[P.MaxRegisteredCollidersPerIteration];
			    int hitCount = Physics2D.BoxCastNonAlloc(transform.position.AsV2(), TrueSize, 0f, step.normalized, hitArray, step.magnitude, CollisionLayers.value);

			    if (hitCount == 0 || hitArray.All(hit => collidersToIgnore.Contains(hit.collider))) break;

			    for (int h = 0; h < hitCount; h++)
			    {
					RaycastHit2D hit = hitArray[h];
					if (collidersToIgnore.Contains(hit.collider) || hit.collider == _box) continue; // hit a previous collider, skip resolve test

					Vector2 hitNormal = hit.normal;
					// Debug.Log($"Hit collider: {hit.collider}, Hit normal: {hitNormal.ToString()}");

					// remember touches
					if (hitNormal.y >= P.MinGroundNormalHeight) newStates.grounded.State = true;
					if (hitNormal.x > 0) newStates.wallLeft.State = true;
					if (hitNormal.x < 0) newStates.wallRight.State = true;
					if (hitNormal.y <= -P.MinGroundNormalHeight) newStates.ceilingAbove.State = true;

					// find impact
					Vector2 impactForceDirection = hit.normal;
					float directionLikeness = Vector2.Dot(impactForceDirection, -step.normalized);
					if (directionLikeness <= 0f) continue;
					float overlapDistance = step.magnitude * (1f - hit.fraction);
					Vector2 impactForce = overlapDistance * directionLikeness * impactForceDirection;

					Vector2 localCorrection;

					// pushing other physics boxes
					PhysicsBox otherPBox = hit.collider.GetComponent<PhysicsBox>();
					if (otherPBox != null)
					{
						Debug.Log($"{name} hit {otherPBox.name} with step: {step.ToString()}, localAccurateImpact: {impactForce.ToString()}, hit normal: {hit.normal.ToString()}");
					}
					if (otherPBox != null && otherPBox.physics.PushPriority < physics.PushPriority)
					{
						// take precedence, but still snap
						localCorrection = Vector2.zero;

						Vector2 otherStep = otherPBox.velocity * Time.fixedDeltaTime;
						Vector2 otherStopDirection = -hit.normal;
						float otherStopDirectionLikeness = Vector2.Dot(-otherStopDirection, otherStep.normalized);
						if (otherStopDirectionLikeness > 0f)
						{
							float otherStopOverlapDistance = 1f;
							Vector2 otherStopImpact = otherStopOverlapDistance * otherStopDirectionLikeness * otherStopDirection;
							Vector2 combinedImpact = otherStopImpact - impactForce;
							otherPBox.CorrectVelocityBy(combinedImpact / Time.fixedDeltaTime);
							Debug.DrawRay(hit.point, combinedImpact, new Color(0.1f, 0.2f, 0f), Time.fixedDeltaTime);
						}
						else
						{
							otherPBox.CorrectVelocityBy(-impactForce / Time.fixedDeltaTime);
						}
					}
					else
					{
						localCorrection = impactForce;
						Debug.DrawRay(hit.point, localCorrection, Color.green, Time.fixedDeltaTime); // show correction for 1 frame
					}

					if (P.Friction.Enabled)
					{
						// friction applied based on impact harshness
						Vector2 rightAxis = Vector2.Perpendicular(hit.normal);
						float stepAxisLikeness = Vector2.Dot(rightAxis, step.normalized);
						float maxFriction = step.magnitude * Mathf.Abs(stepAxisLikeness);
						float frictionStrength = maxFriction * P.Friction.Value;
						if (P.MinimumFriction.Enabled) frictionStrength = Mathf.Max(P.MinimumFriction, frictionStrength);
						float frictionAmount = Mathf.MoveTowards(0f, maxFriction, frictionStrength * Time.fixedDeltaTime);
						Vector2 frictionDirection = rightAxis * stepAxisLikeness.AsIntSign();
						Vector2 friction = frictionAmount * frictionDirection;
						localCorrection += friction;
					}

					step += localCorrection; // apply correction
					collidersToIgnore.Add(hit.collider);
			    }
		    }
		    // update states
		    CollisionStates.TransferStates(newStates);

		    return step;
	    }

	    private void CorrectVelocityBy(Vector2 amount)
	    {
		    velocity += amount; // enough to stop, and then push
	    }

/*
	    private Vector2 GetSmallestVectorToObstacle(RaycastHit2D hit)
	    {
		    Vector2 distancesFromCenter = hit.point - transform.position.AsV2();
		    Vector2 offset = Vector2.Min(TrueSize, distancesFromCenter);
		    return distancesFromCenter - offset;
	    }
*/

	    private protected float HeightToUpwardsVelocity(float height) => P.Gravity.Enabled ? Mathf.Sqrt(2 * height * P.Gravity) : height;

	    private protected bool OverlapBoxForOtherColliders(Vector2 position, Vector2 size)
	    {
		    Collider2D[] colliders = new Collider2D[2];
		    Physics2D.OverlapBoxNonAlloc(position, size, 0f, colliders);
		    return colliders.Any(hitCollider => hitCollider != null && hitCollider != Box);
	    }

	    private protected RaycastHit2D BoxCastForOtherColliders(Vector2 origin, Vector2 size, Vector2 direction, float maxDistance)
	    {
		    RaycastHit2D[] colliders = new RaycastHit2D[2];
		    Physics2D.BoxCastNonAlloc(origin, size, 0f, direction, colliders, maxDistance, CollisionLayers.value);
		    return new RaycastHit2D();
	    }

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