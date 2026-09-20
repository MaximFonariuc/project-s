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
        [SerializeField, Min(0.001f)] private float _rungSpacing = 0.65f;
        [SerializeField] private float _firstRungOffset = 0.53f;
        [SerializeField, Min(0.0f)] private float _rungHalfWidth = 0.18f;

        public Vector3 BottomAnchorPoint => transform.position + transform.TransformVector(_pathOffset);
        public Vector3 TopAnchorPoint => BottomAnchorPoint + transform.up * _pathLength;

        public Transform TopPoint => _topPoint;
        public Transform BottomPoint => _bottomPoint;

        public Vector3 ClimbAxis => transform.up;
        public Quaternion ClimbRotation => transform.rotation;
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

        public float HeightAlongPath(Vector3 position)
        {
            return Vector3.Dot(position - BottomAnchorPoint, ClimbAxis.normalized);
        }

        public Vector3 NearestRungPoint(Vector3 worldPosition)
        {
            float spacing = RungSpacing;
            Vector3 axis = ClimbAxis.normalized;
            Vector3 origin = BottomAnchorPoint + axis * (_firstRungOffset * Mathf.Abs(transform.lossyScale.y));
            float height = Vector3.Dot(worldPosition - origin, axis);
            if (spacing > 0.0001f)
            {
                height = Mathf.Round(height / spacing) * spacing;
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

            if (_bottomPoint == null || _topPoint == null)
                return;

            Gizmos.DrawWireCube(_bottomPoint.position, Vector3.one * 0.25f);
            Gizmos.DrawWireCube(_topPoint.position, Vector3.one * 0.25f);
        }
    }
}