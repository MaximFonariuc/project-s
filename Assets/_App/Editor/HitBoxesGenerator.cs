using UnityEditor;
using UnityEngine;

public class HitBoxesGenerator : MonoBehaviour
{
	[MenuItem("Tools/HitBoxesGenerator/Generate Hit Boxes")]
	private static void Generate()
	{
		GameObject selected = Selection.activeGameObject;
		if (selected == null)
		{
			Debug.LogWarning("Выберите персонажа.");
			return;
		}

		Animator animator = selected.GetComponentInChildren<Animator>();
		if (animator == null || !animator.isHuman)
		{
			Debug.LogWarning("Animator не найден или не Humanoid.");
			return;
		}

		ClearAllColliders();
		
		GenerateTorso(animator);
		CreateHeadCollider(animator);

		// Руки
		CreateSegmentWithPadding(animator, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, radiusMultiplier: 0.15f, padding: 0.02f);
		CreateSegmentWithPadding(animator, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, radiusMultiplier: 0.12f, padding: 0.02f);
		CreateSegmentWithPadding(animator, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, radiusMultiplier: 0.15f, padding: 0.02f);
		CreateSegmentWithPadding(animator, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, radiusMultiplier: 0.12f, padding: 0.02f);

		// Ноги
		CreateSegmentWithPadding(animator, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, radiusMultiplier: 0.20f, padding: 0.03f);
		CreateSegmentWithPadding(animator, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, radiusMultiplier: 0.15f, padding: 0.03f);
		CreateSegmentWithPadding(animator, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, radiusMultiplier: 0.20f, padding: 0.03f);
		CreateSegmentWithPadding(animator, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, radiusMultiplier: 0.15f, padding: 0.03f);

		Debug.Log("HitBoxes сгенерированы.");
	}

	[MenuItem("Tools/HitBoxesGenerator/Clear All Colliders")]
	private static void ClearAllColliders()
	{
		GameObject selected = Selection.activeGameObject;
		if (selected == null)
		{
			Debug.LogWarning("Выберите объект для очистки.");
			return;
		}

		int removed = 0;
		Animator animator = selected.GetComponentInChildren<Animator>();
		
		foreach (var col in animator.GetComponentsInChildren<Collider>(true))
		{
			DestroyImmediate(col);
			removed++;
		}

		Debug.Log($"Удалено всех коллайдеров: {removed} из {selected.name}");
	}
	
	[MenuItem("Tools/HitBoxesGenerator/Set Hitbox Layer For Colliders")]
	private static void SetHitboxLayer()
	{
		GameObject selected = Selection.activeGameObject;
		if (selected == null)
		{
			Debug.LogWarning("Выберите объект с Animator.");
			return;
		}

		Animator animator = selected.GetComponentInChildren<Animator>();
		if (animator == null)
		{
			Debug.LogWarning("Animator не найден.");
			return;
		}

		int hitboxLayer = LayerMask.NameToLayer("Hitbox");
		if (hitboxLayer == -1)
		{
			Debug.LogError("Слой 'Hitbox' не существует. Создай его в Project Settings > Tags and Layers.");
			return;
		}

		int changed = 0;
		foreach (var col in animator.GetComponentsInChildren<Collider>(true))
		{
			col.gameObject.layer = hitboxLayer;
			changed++;
		}

		Debug.Log($"Всем коллайдерам персонажа присвоен слой 'Hitbox'. Коллайдеров изменено: {changed}");
	}

	private static void CreateSegmentWithPadding(Animator animator, HumanBodyBones startBone, HumanBodyBones endBone, float radiusMultiplier = 0.15f, float padding = 0.02f)
	{
		Transform start = animator.GetBoneTransform(startBone);
		Transform end = animator.GetBoneTransform(endBone);
		if (start == null || end == null) return;

		Vector3 startPos = start.position;
		Vector3 endPos = end.position;
		Vector3 worldDirection = endPos - startPos;
		float length = worldDirection.magnitude;
		if (length <= 0.001f) return;

		CapsuleCollider col = start.GetComponent<CapsuleCollider>();
		if (col == null)
			col = start.gameObject.AddComponent<CapsuleCollider>();

		Vector3 localDir = start.InverseTransformDirection(worldDirection.normalized);

		int direction;
		float absX = Mathf.Abs(localDir.x);
		float absY = Mathf.Abs(localDir.y);
		float absZ = Mathf.Abs(localDir.z);
		if (absX > absY && absX > absZ) direction = 0;
		else if (absY > absX && absY > absZ) direction = 1;
		else direction = 2;

		Vector3 lossy = start.lossyScale;
		float axisScale = direction == 0 ? lossy.x : (direction == 1 ? lossy.y : lossy.z);

		col.direction = direction;
		col.height = (length / axisScale) + (padding * 2f);

		col.radius = (length * radiusMultiplier) / Mathf.Max(lossy.x, lossy.y, lossy.z);
		col.center = start.InverseTransformPoint((startPos + endPos) / 2f);
		col.isTrigger = true;
	}

	private static void GenerateTorso(Animator animator)
	{
		Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
		Transform spine = animator.GetBoneTransform(HumanBodyBones.Spine);
		Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
		Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck);

		if (hips != null && spine != null)
			CreateSegmentWithPadding(animator, HumanBodyBones.Hips, HumanBodyBones.Spine, radiusMultiplier: 0.25f, padding: 0.02f);

		if (spine != null && chest != null)
			CreateSegmentWithPadding(animator, HumanBodyBones.Spine, HumanBodyBones.Chest, radiusMultiplier: 0.25f, padding: 0.02f);

		if (chest != null && neck != null)
			CreateSegmentWithPadding(animator, HumanBodyBones.Chest, HumanBodyBones.Neck, radiusMultiplier: 0.25f, padding: 0.02f);
	}

	private static void CreateHeadCollider(Animator animator)
	{
		Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
		Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck);
		if (head == null || neck == null) return;

		float worldLength = Vector3.Distance(neck.position, head.position);

		// короткая капсула вокруг головы
		CapsuleCollider col = head.GetComponent<CapsuleCollider>();
		if (col == null)
			col = head.gameObject.AddComponent<CapsuleCollider>();

		col.direction = 1; // Y-вверх
		// высота чуть больше расстояния Neck->Head, но близка к диаметру
		col.height = worldLength * 1.5f;
		col.radius = worldLength * 0.45f;
		col.center = head.InverseTransformPoint((neck.position + head.position) / 2f);

		col.isTrigger = true;
	}

	[RequireComponent(typeof(Rigidbody))]
	[RequireComponent(typeof(Collider))]
	public class RagdollBone : MonoBehaviour
	{
		public Transform parentBone;
	}

	public enum BodyPart
	{
		Head,
		Torso,
		UpperArm,
		LowerArm,
		UpperLeg,
		LowerLeg
	}
}
