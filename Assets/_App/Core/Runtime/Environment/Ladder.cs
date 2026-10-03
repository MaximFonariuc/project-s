using UnityEngine;

namespace _App
{
    public sealed class Ladder : MonoBehaviour, IInteractable
    {
        [Header("Ladder Path")]
        [SerializeField] private float _pathLength = 10.0f;
        [SerializeField] private Vector3 _pathOffset = new Vector3(0f, 0f, -0.5f);

        [Header("Anchor Points")]
        [SerializeField] private Transform _topPoint;
        [SerializeField] private Transform _bottomPoint;

        [Header("Rungs")]
        [SerializeField] private Vector3 _firstRungOffset = new Vector3(0.0f, 0.3075f, 0.0125f);
        [SerializeField, Min(0.001f)] private float _rungSpacing = 0.65f;
        [SerializeField, Min(0.0f)] private float _rungHalfWidth = 0.45f;
        [SerializeField, Min(1)] private int _rungCount = 19;

        public Vector3 BottomAnchorPoint => transform.position + transform.TransformVector(_pathOffset);
        public Vector3 TopAnchorPoint => BottomAnchorPoint + transform.up * _pathLength;

        public Transform TopPoint => _topPoint;
        public Transform BottomPoint => _bottomPoint;

        public Vector3 ClimbAxis => transform.up;
        public Quaternion ClimbRotation => transform.rotation;
        public float PathLength => _pathLength;
        public float RungSpacing => _rungSpacing * Mathf.Abs(transform.lossyScale.y);

        public float ResolveStepHeight(float authoredCycleRise)
        {
            float spacing = RungSpacing;
            if (spacing <= 0.0001f || authoredCycleRise <= 0.0001f)
            {
                return authoredCycleRise;
            }

            int rungs = Mathf.Max(1, Mathf.RoundToInt(authoredCycleRise / spacing));
            return rungs * spacing;
        }

        public float ResolvePathLength(float stepHeight)
        {
            if (stepHeight <= 0.0001f)
            {
                return _pathLength;
            }

            int steps = Mathf.Max(1, Mathf.RoundToInt(_pathLength / stepHeight));
            return steps * stepHeight;
        }

        public float HeightAlongPath(Vector3 position)
        {
            return Vector3.Dot(position - BottomAnchorPoint, ClimbAxis.normalized);
        }

        public Vector3 PointAtHeight(float height)
        {
            return BottomAnchorPoint + ClimbAxis.normalized * height;
        }

        public Vector3 NearestRungPoint(Vector3 worldPosition)
        {
            Vector3 axis = ClimbAxis.normalized;
            Vector3 origin = transform.TransformPoint(_firstRungOffset);
            float spacing = RungSpacing;
            float height = Vector3.Dot(worldPosition - origin, axis);
            if (spacing > 0.0001f)
            {
                int rung = Mathf.Clamp(Mathf.RoundToInt(height / spacing), 0, _rungCount - 1);
                height = rung * spacing;
            }

            Vector3 onAxis = origin + axis * height;
            Vector3 lateral = transform.right.normalized;
            float side = Vector3.Dot(worldPosition - onAxis, lateral);
            float halfWidth = _rungHalfWidth * Mathf.Abs(transform.lossyScale.x);
            return onAxis + lateral * Mathf.Clamp(side, -halfWidth, halfWidth);
        }

        public Transform ClosestPointOnPath(Vector3 position, out float pathPosition)
        {
            Vector3 path = TopAnchorPoint - BottomAnchorPoint;
            Vector3 pathToPoint = position - BottomAnchorPoint;

            float height = Vector3.Dot(pathToPoint, path.normalized);

            if (height > 0.0f)
            {
                pathPosition = height - path.magnitude;
                return TopPoint;
            }

            pathPosition = height;
            return BottomPoint;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(BottomAnchorPoint, TopAnchorPoint);

            Gizmos.color = Color.cyan;
            Vector3 origin = transform.TransformPoint(_firstRungOffset);
            Vector3 axis = ClimbAxis.normalized;
            Vector3 lateral = transform.right.normalized * (_rungHalfWidth * Mathf.Abs(transform.lossyScale.x));
            float spacing = RungSpacing;
            for (int i = 0; i < _rungCount; i++)
            {
                Vector3 center = origin + axis * (i * spacing);
                Gizmos.DrawLine(center - lateral, center + lateral);
            }

            if (_bottomPoint == null || _topPoint == null)
                return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(_bottomPoint.position, Vector3.one * 0.25f);
            Gizmos.DrawWireCube(_topPoint.position, Vector3.one * 0.25f);
        }
    }
}