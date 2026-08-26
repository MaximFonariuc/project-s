using UnityEngine;

namespace _App
{
    internal interface IWeaponHitboxSweepConsumer
    {
        void ProcessHitboxCandidate(Collider other);
    }

    internal sealed class WeaponHitboxSweep
    {
        private const int OverlapCapacity = 32;
        private const float MinimumStepDistance = 0.02f;

        private readonly Collider _hitbox;
        private readonly int _layerMask;
        private readonly QueryTriggerInteraction _triggerInteraction;
        private readonly int _maxSteps;
        private readonly Collider[] _overlaps = new Collider[OverlapCapacity];

        private Vector3 _previousCenter;
        private Vector3 _previousPointA;
        private Vector3 _previousPointB;
        private Vector3 _previousHalfExtents;
        private Quaternion _previousRotation;
        private float _previousRadius;
        private bool _initialized;

        public WeaponHitboxSweep(
            Collider hitbox,
            LayerMask layerMask,
            QueryTriggerInteraction triggerInteraction,
            int maxSteps)
        {
            _hitbox = hitbox;
            _layerMask = layerMask;
            _triggerInteraction = triggerInteraction;
            _maxSteps = Mathf.Max(1, maxSteps);
        }

        public void Begin()
        {
            _initialized = false;
            CaptureCurrentShape();
        }

        public void Reset()
        {
            _initialized = false;
        }

        public bool Sample(IWeaponHitboxSweepConsumer consumer)
        {
            if (_hitbox == null || consumer == null)
            {
                return false;
            }

            return _hitbox switch
            {
                CapsuleCollider capsule => SampleCapsule(capsule, consumer),
                SphereCollider sphere => SampleSphere(sphere, consumer),
                BoxCollider box => SampleBox(box, consumer),
                _ => false
            };
        }

        private bool SampleCapsule(
            CapsuleCollider capsule,
            IWeaponHitboxSweepConsumer consumer)
        {
            GetCapsule(capsule, out Vector3 pointA, out Vector3 pointB, out float radius);
            if (!_initialized)
            {
                _previousPointA = pointA;
                _previousPointB = pointB;
                _previousRadius = radius;
                _initialized = true;
            }

            float travel = Mathf.Max(
                Vector3.Distance(_previousPointA, pointA),
                Vector3.Distance(_previousPointB, pointB));
            int steps = CalculateSteps(travel, Mathf.Max(radius, _previousRadius));
            bool saturated = false;
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                int count = Physics.OverlapCapsuleNonAlloc(
                    Vector3.Lerp(_previousPointA, pointA, t),
                    Vector3.Lerp(_previousPointB, pointB, t),
                    Mathf.Lerp(_previousRadius, radius, t),
                    _overlaps,
                    _layerMask,
                    _triggerInteraction);
                saturated |= Consume(count, consumer);
            }

            _previousPointA = pointA;
            _previousPointB = pointB;
            _previousRadius = radius;
            return saturated;
        }

        private bool SampleSphere(
            SphereCollider sphere,
            IWeaponHitboxSweepConsumer consumer)
        {
            GetSphere(sphere, out Vector3 center, out float radius);
            if (!_initialized)
            {
                _previousCenter = center;
                _previousRadius = radius;
                _initialized = true;
            }

            float travel = Vector3.Distance(_previousCenter, center);
            int steps = CalculateSteps(travel, Mathf.Max(radius, _previousRadius));
            bool saturated = false;
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                int count = Physics.OverlapSphereNonAlloc(
                    Vector3.Lerp(_previousCenter, center, t),
                    Mathf.Lerp(_previousRadius, radius, t),
                    _overlaps,
                    _layerMask,
                    _triggerInteraction);
                saturated |= Consume(count, consumer);
            }

            _previousCenter = center;
            _previousRadius = radius;
            return saturated;
        }

        private bool SampleBox(
            BoxCollider box,
            IWeaponHitboxSweepConsumer consumer)
        {
            GetBox(box, out Vector3 center, out Vector3 halfExtents, out Quaternion rotation);
            if (!_initialized)
            {
                _previousCenter = center;
                _previousHalfExtents = halfExtents;
                _previousRotation = rotation;
                _initialized = true;
            }

            float angularTravel = Quaternion.Angle(_previousRotation, rotation) *
                                  Mathf.Deg2Rad *
                                  Mathf.Max(_previousHalfExtents.magnitude, halfExtents.magnitude);
            float travel = Vector3.Distance(_previousCenter, center) + angularTravel;
            float stepDistance = Mathf.Max(
                MinimumStepDistance,
                Mathf.Min(halfExtents.x, Mathf.Min(halfExtents.y, halfExtents.z)));
            int steps = CalculateSteps(travel, stepDistance);
            bool saturated = false;
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                int count = Physics.OverlapBoxNonAlloc(
                    Vector3.Lerp(_previousCenter, center, t),
                    Vector3.Lerp(_previousHalfExtents, halfExtents, t),
                    _overlaps,
                    Quaternion.Slerp(_previousRotation, rotation, t),
                    _layerMask,
                    _triggerInteraction);
                saturated |= Consume(count, consumer);
            }

            _previousCenter = center;
            _previousHalfExtents = halfExtents;
            _previousRotation = rotation;
            return saturated;
        }

        private bool Consume(int count, IWeaponHitboxSweepConsumer consumer)
        {
            int safeCount = Mathf.Min(count, _overlaps.Length);
            for (int i = 0; i < safeCount; i++)
            {
                Collider candidate = _overlaps[i];
                _overlaps[i] = null;
                if (candidate != null && candidate != _hitbox)
                {
                    consumer.ProcessHitboxCandidate(candidate);
                }
            }

            return count >= _overlaps.Length;
        }

        private int CalculateSteps(float travel, float shapeRadius)
        {
            float stepDistance = Mathf.Max(MinimumStepDistance, shapeRadius);
            return Mathf.Clamp(Mathf.CeilToInt(travel / stepDistance), 1, _maxSteps);
        }

        private void CaptureCurrentShape()
        {
            switch (_hitbox)
            {
                case CapsuleCollider capsule:
                    GetCapsule(capsule, out _previousPointA, out _previousPointB, out _previousRadius);
                    _initialized = true;
                    break;
                case SphereCollider sphere:
                    GetSphere(sphere, out _previousCenter, out _previousRadius);
                    _initialized = true;
                    break;
                case BoxCollider box:
                    GetBox(box, out _previousCenter, out _previousHalfExtents, out _previousRotation);
                    _initialized = true;
                    break;
            }
        }

        private static void GetCapsule(
            CapsuleCollider capsule,
            out Vector3 pointA,
            out Vector3 pointB,
            out float radius)
        {
            Transform transform = capsule.transform;
            Vector3 scale = Abs(transform.lossyScale);
            Vector3 localAxis = capsule.direction switch
            {
                0 => Vector3.right,
                2 => Vector3.forward,
                _ => Vector3.up
            };
            float axisScale = capsule.direction switch
            {
                0 => scale.x,
                2 => scale.z,
                _ => scale.y
            };
            float radiusScale = capsule.direction switch
            {
                0 => Mathf.Max(scale.y, scale.z),
                2 => Mathf.Max(scale.x, scale.y),
                _ => Mathf.Max(scale.x, scale.z)
            };

            radius = capsule.radius * radiusScale;
            float halfSegment = Mathf.Max(0.0f, capsule.height * axisScale * 0.5f - radius);
            Vector3 center = transform.TransformPoint(capsule.center);
            Vector3 axis = transform.TransformDirection(localAxis).normalized;
            pointA = center + axis * halfSegment;
            pointB = center - axis * halfSegment;
        }

        private static void GetSphere(
            SphereCollider sphere,
            out Vector3 center,
            out float radius)
        {
            Transform transform = sphere.transform;
            Vector3 scale = Abs(transform.lossyScale);
            center = transform.TransformPoint(sphere.center);
            radius = sphere.radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
        }

        private static void GetBox(
            BoxCollider box,
            out Vector3 center,
            out Vector3 halfExtents,
            out Quaternion rotation)
        {
            Transform transform = box.transform;
            center = transform.TransformPoint(box.center);
            halfExtents = Vector3.Scale(box.size * 0.5f, Abs(transform.lossyScale));
            rotation = transform.rotation;
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(
                Mathf.Abs(value.x),
                Mathf.Abs(value.y),
                Mathf.Abs(value.z));
        }
    }
}
