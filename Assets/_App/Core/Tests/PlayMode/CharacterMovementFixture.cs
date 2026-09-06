using System.Collections.Generic;
using ModularCharacterMovement;
using UnityEngine;

namespace ProjectS.Core.PlayModeTests
{
    internal sealed class CharacterMovementFixture
    {
        public const float Radius = 0.5f;
        public const float Height = 2.0f;

        private readonly List<GameObject> _spawned = new List<GameObject>();

        public CharacterMovement Movement { get; private set; }

        public CharacterMovement SpawnCharacter(Vector3 position)
        {
            var go = new GameObject("TestCharacter");
            _spawned.Add(go);
            go.transform.position = position;

            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            CapsuleCollider capsule = go.AddComponent<CapsuleCollider>();
            capsule.radius = Radius;
            capsule.height = Height;
            capsule.center = new Vector3(0.0f, Height * 0.5f, 0.0f);

            Movement = go.AddComponent<CharacterMovement>();
            Movement.SetDimensions(Radius, Height);
            Movement.SlopeLimit = 45.0f;
            Movement.StepOffset = 0.3f;
            Movement.SetPosition(position, updateGround: true);
            Physics.SyncTransforms();
            return Movement;
        }

        public GameObject SpawnBox(Vector3 center, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _spawned.Add(go);
            go.name = "TestBox";
            go.transform.position = center;
            go.transform.localScale = size;
            Object.Destroy(go.GetComponent<MeshRenderer>());
            Physics.SyncTransforms();
            return go;
        }

        public GameObject SpawnGround(Vector3 center, float extent = 40.0f)
        {
            return SpawnBox(center, new Vector3(extent, 1.0f, extent));
        }

        public GameObject SpawnSlope(Vector3 center, float degrees, Vector3 size)
        {
            GameObject slope = SpawnBox(center, size);
            slope.transform.rotation = Quaternion.Euler(0.0f, 0.0f, degrees);
            Physics.SyncTransforms();
            return slope;
        }

        public void Settle(int steps = 20, float deltaTime = 0.02f)
        {
            for (int i = 0; i < steps; i++)
            {
                Movement.Move(new Vector3(0.0f, -4.0f, 0.0f), deltaTime);
                if (Movement.IsGrounded)
                {
                    Movement.Move(Vector3.zero, deltaTime);
                    return;
                }
            }
        }

        public void Advance(Vector3 velocity, int steps = 1, float deltaTime = 0.02f)
        {
            for (int i = 0; i < steps; i++)
            {
                Movement.Move(velocity, deltaTime);
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                {
                    Object.DestroyImmediate(_spawned[i]);
                }
            }

            _spawned.Clear();
            Movement = null;
        }
    }
}
