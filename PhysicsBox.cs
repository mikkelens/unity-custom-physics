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
    [SuppressMessage("ReSharper", "Unity.InefficientPropertyAccess")] // I don't care about transform.position warnings
	[SelectionBase]
    [RequireComponent(typeof(BoxCollider2D))]
	public abstract class PhysicsBox : MonoBehaviour
	{
		[Header("Physics Settings")]
		[SerializeField] private PhysicsSettings physics; // see below

		private PhysicsSettings P => physics;

	    [Header("Physics Debug")]
	    [SerializeField] private Optional<Vector2> startVelocity;
	    [SerializeField] private Optional<float> debugFixedDeltaTime = new Optional<float>(0.25f); // delay between physics steps, default is 4 updates per second
	    [SerializeField] private Optional<float> debugTimeScale = new Optional<float>(0.5f); // slow down time, default is half speed
	    [SerializeField, ReadOnly] private protected Vector2 physicsVelocity;

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
		    if (startVelocity.Enabled) physicsVelocity = startVelocity.Value;
	    }

	    private protected virtual void Start()
	    {
		    CollisionStates = new CollisionStates();
	    }

	    private protected virtual float CurrentGravity => P.Gravity.Value;

	    private void FixedUpdate()
	    {
		    // debug
		    if (debugFixedDeltaTime.Enabled) Time.fixedDeltaTime = debugFixedDeltaTime.Value; // may not work...
		    if (debugTimeScale.Enabled) Time.timeScale = debugTimeScale.Value;

		    // physics sandbox settings
		    if (P.Gravity.Enabled) physicsVelocity.y -= CurrentGravity * Time.fixedDeltaTime;
		    if (P.MaxFallSpeed.Enabled) physicsVelocity.y = Mathf.Max(physicsVelocity.y, -P.MaxFallSpeed);

		    physicsVelocity = UpdateVelocity(physicsVelocity);

		    // Velocity fix as collision handling. think this is called continuous interpolated physics?
		    Vector2 step = physicsVelocity * Time.fixedDeltaTime;

		    step = CorrectStep(step);

		    // apply physics movement
		    // if (step.magnitude >= P.MinimumMoveDistance)
		    {
			    transform.Translate(step);

			    // Vector3 latePos = transform.position;
				// Debug.DrawLine(latePos, latePos + velocity.AsV3() * Time.fixedDeltaTime, Color.red, Time.fixedDeltaTime); // prediction line
		    }

		    // remember velocity from step
		    physicsVelocity = step / Time.fixedDeltaTime;
	    }

	    private protected abstract Vector2 UpdateVelocity(Vector2 newVelocity);

	    private Vector2 CorrectStep(Vector2 step)
	    {
		    // reset collision dependent states
		    CollisionStates newStates = new CollisionStates(); // false by default

		    // make sure we only deal with the same collider once per physics step
		    List<Collider2D> collidersToIgnore = new List<Collider2D>();
		    for (int i = 0; i < P.MaxCollisionPreventionIterations; i++) // each iteration
		    {
			    if (step.magnitude == 0f) break; // this is not collision resolution, but prevention

			    RaycastHit2D[] hitArray = new RaycastHit2D[P.MaxRegisteredCollidersPerIteration];
			    int hitCount = Physics2D.BoxCastNonAlloc(transform.position.AsV2(), TrueSize, 0f, step.normalized, hitArray, step.magnitude, CollisionLayers.value);

			    if (hitCount == 0 || hitArray.All(hit => collidersToIgnore.Contains(hit.collider))) break;

			    for (int h = 0; h < hitCount; h++)
			    {
					RaycastHit2D hit = hitArray[h];
					if (collidersToIgnore.Contains(hit.collider) || hit.collider == _box) continue; // hit a previous collider, skip resolve test
					collidersToIgnore.Add(hit.collider);

					// remember touches
					if (hit.normal.y > 0f) newStates.grounded.State = true;
					if (hit.normal.y <= -P.MinGroundNormalHeight) newStates.ceilingAbove.State = true;
					if (hit.normal.x > 0) newStates.wallLeft.State = true;
					else if (hit.normal.x < 0) newStates.wallRight.State = true;

					// if (hit.fraction == 0f) continue; // exactly next to thing

				    step = HandleCollision(step, hit);
			    }
		    }
		    // update states
		    CollisionStates.UpdateStates(newStates);

		    return step;
	    }

	    private protected virtual Vector2 HandleCollision(Vector2 newStep, RaycastHit2D hit)
	    {
		    float overlapFraction = 1f - hit.fraction;
		    Vector2 entryNormal = -hit.normal;
		    float stepEntryRelevance = Vector2.Dot(entryNormal, newStep.normalized);
		    if (stepEntryRelevance <= 0f) return newStep;
		    float entryDistance = newStep.magnitude * stepEntryRelevance * overlapFraction;

		    PhysicsBox foreignPhysicsBox = hit.transform.GetComponent<PhysicsBox>();
		    if (foreignPhysicsBox != null && foreignPhysicsBox.physics.PushPriority < physics.PushPriority)
		    {
			    if (foreignPhysicsBox is not PhysicsBoxHazard)
			    {
				    // push other object - the amount we are pushing by will be added to its velocity, after also stopping it
					foreignPhysicsBox.ConformToVelocity(physicsVelocity);
					newStep += entryDistance * hit.normal;
			    }
			    else
			    {
				    // simply do nothing (hazard handles damage)
			    }
		    }
		    else
		    {
			    // simply correct self
			    newStep += entryDistance * hit.normal;
		    }

		    if (P.CollisionFriction.Enabled) // todo: make friction above a value of 1 work?
		    {
			    // friction applied based on impact harshness
			    Vector2 rightAxis = Vector2.Perpendicular(hit.normal);
			    float stepAxisLikeness = Vector2.Dot(rightAxis, -newStep.normalized);
			    float frictionTarget = newStep.magnitude * Mathf.Abs(stepAxisLikeness);
			    float frictionAmount = frictionTarget * P.CollisionFriction.Value;
			    if (P.MinimumCollisionFriction.Enabled) frictionAmount = Mathf.Max(P.MinimumCollisionFriction, frictionAmount);
			    float frictionStrength = Mathf.MoveTowards(0f, frictionTarget, frictionAmount * Time.fixedDeltaTime);
			    Vector2 frictionDirection = rightAxis * stepAxisLikeness.AsIntSign();
			    Vector2 friction = frictionStrength * frictionDirection;
			    newStep += friction;
		    }
		    return newStep;
	    }

	    private void ConformToVelocity(Vector2 conformVelocity) // used by pushing object
	    {
		    physicsVelocity.x = physicsVelocity.x.SignedMax(conformVelocity.x);
		    physicsVelocity.y = physicsVelocity.y.SignedMax(conformVelocity.y);
		    // todo: return how well the thing conformed,
		    // to allow partial stopping of pushing collider
	    }

/*
	    private Vector2 GetSmallestVectorToObstacle(RaycastHit2D hit)
	    {
		    Vector2 distancesFromCenter = hit.point - transform.position.AsV2();
		    Vector2 offset = Vector2.Min(TrueSize, distancesFromCenter);
		    return distancesFromCenter - offset;
	    }
*/

	    private protected float HeightToUpwardsVelocity(float height) => P.Gravity.Enabled ? Mathf.Sqrt(2 * height * CurrentGravity) : height;

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

		    Vector2 posNextFrame = pos + physicsVelocity * Time.fixedDeltaTime;
		    Gizmos.color = Color.blue;
		    Gizmos.DrawWireCube(posNextFrame, TrueSize);
	    }
    }
}