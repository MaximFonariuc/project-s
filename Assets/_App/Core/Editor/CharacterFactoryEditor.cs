using UnityEditor;
using UnityEngine;
using ModularCharacterMovement;

namespace _App
{
    public static class CharacterFactoryEditor
    {
        private const string PATH = "GameObject/Characters/";
        private const int PRIORITY = 1;

        private static void InitPhysicsBody(GameObject go)
        {
            Rigidbody rb = go.GetComponent<Rigidbody>();

            rb.linearDamping = 0.0f;
            rb.angularDamping = 0.0f;
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            CapsuleCollider capsuleCollider = go.GetComponent<CapsuleCollider>();

            capsuleCollider.center = new Vector3(0f, 1f, 0f);
            capsuleCollider.radius = 0.5f;
            capsuleCollider.height = 2.0f;
        }
        
        [MenuItem(PATH + "Character", false, PRIORITY)]
        public static void CreateCharacter()
        {
            GameObject go = new GameObject("Character", typeof(Rigidbody), typeof(CapsuleCollider),
                typeof(CharacterMovement), typeof(Character));

            InitPhysicsBody(go);
            
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);

            Selection.activeGameObject = go;
            SceneView.FrameLastActiveSceneView();
        }
    }
}