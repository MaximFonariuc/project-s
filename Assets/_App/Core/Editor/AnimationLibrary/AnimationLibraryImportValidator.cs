using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectS.Core.Editor.AnimationLibrary
{
    internal static class AnimationLibraryImportValidator
    {
        private const string MenuPath = "Tools/Project S/Animation Library/Validate Phase 9 Pilot";

        [Serializable]
        private sealed class Receipt
        {
            public int SchemaVersion = 1;
            public string Status;
            public string UnityVersion;
            public string Timestamp;
            public string ImportRoot;
            public ImportResult[] Imports;
        }

        [Serializable]
        private sealed class ImportResult
        {
            public string LibraryID;
            public string AnimationID;
            public string AssetPath;
            public string AssetGuid;
            public string FBXSHA256;
            public string AnimationType;
            public string AvatarSetup;
            public string SourceAvatarPath;
            public string TargetAvatarSHA256;
            public string RootMotionStatus;
            public int ExpectedFrameCount;
            public int ImportedClipCount;
            public string[] ImportedClipNames;
            public float[] ImportedClipLengths;
            public int ExpectedAnimatedBoneCount;
            public int MinimumNonConstantPoseCurves;
            public int NonConstantPoseCurveCount;
            public string PoseMotionValidation;
            public string[] Labels;
            public string Status;
            public string[] Failures;
        }

        [MenuItem(MenuPath, false, 2300)]
        private static void ValidatePhase9Pilot()
        {
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { AnimationLibraryMetadataReader.ImportRoot.TrimEnd('/') });
            var results = new List<ImportResult>(guids.Length);
            bool allPassed = guids.Length > 0;

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ImportResult result = ValidateImport(assetPath, guid);
                results.Add(result);
                allPassed &= string.Equals(result.Status, "PASS", StringComparison.Ordinal);
            }

            if (results.Count != 5)
            {
                allPassed = false;
            }

            var receipt = new Receipt
            {
                Status = allPassed ? "PASS" : "FAIL",
                UnityVersion = Application.unityVersion,
                Timestamp = DateTime.UtcNow.ToString("O"),
                ImportRoot = AnimationLibraryMetadataReader.ImportRoot,
                Imports = results.ToArray(),
            };
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outputDirectory = Path.Combine(projectRoot, "RPG_Animation_Library", "08_UNITY", "phase_9");
            Directory.CreateDirectory(outputDirectory);
            string receiptPath = Path.Combine(outputDirectory, "unity_import_receipt.json");
            File.WriteAllText(receiptPath, JsonUtility.ToJson(receipt, true) + Environment.NewLine);

            if (allPassed)
            {
                Debug.Log($"[AnimationLibrary] Phase 9 import validation PASS ({results.Count} clips). Receipt: {receiptPath}");
            }
            else
            {
                Debug.LogError($"[AnimationLibrary] Phase 9 import validation FAIL. Receipt: {receiptPath}");
            }
        }

        private static ImportResult ValidateImport(string assetPath, string guid)
        {
            var failures = new List<string>();
            AnimationLibraryMetadataReader.TryReadForModel(
                assetPath,
                out AnimationLibraryImportMetadata metadata,
                out string metadataError);
            if (metadata == null)
            {
                failures.Add(metadataError ?? "Metadata is missing.");
                return new ImportResult
                {
                    AssetPath = assetPath,
                    AssetGuid = guid,
                    Status = "FAIL",
                    Failures = failures.ToArray(),
                };
            }

            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
            {
                failures.Add("ModelImporter is missing.");
            }
            else
            {
                if (importer.animationType != ModelImporterAnimationType.Human)
                {
                    failures.Add($"animationType={importer.animationType}");
                }

                if (importer.avatarSetup != ModelImporterAvatarSetup.CopyFromOther)
                {
                    failures.Add($"avatarSetup={importer.avatarSetup}");
                }

                if (importer.sourceAvatar == null || !importer.sourceAvatar.isValid || !importer.sourceAvatar.isHuman)
                {
                    failures.Add("sourceAvatar is missing or invalid.");
                }

                if (!string.Equals(
                        AssetDatabase.GetAssetPath(importer.sourceAvatar),
                        metadata.TargetAvatarAsset,
                        StringComparison.Ordinal))
                {
                    failures.Add("sourceAvatar path mismatch.");
                }

                ModelImporterClipAnimation[] settings = importer.clipAnimations;
                if (settings == null || settings.Length == 0)
                {
                    failures.Add("clipAnimations is empty.");
                }
                else
                {
                    foreach (ModelImporterClipAnimation clip in settings)
                    {
                        if (clip.loopTime != metadata.Loop || clip.loopPose != metadata.Loop)
                        {
                            failures.Add($"loop policy mismatch on {clip.name}.");
                        }

                        bool expectedLock = !metadata.HasRootMotion;
                        if (clip.lockRootRotation != expectedLock ||
                            clip.lockRootHeightY != expectedLock ||
                            clip.lockRootPositionXZ != expectedLock)
                        {
                            failures.Add($"root policy mismatch on {clip.name}.");
                        }

                        int importedFrames = Mathf.RoundToInt(clip.lastFrame - clip.firstFrame) + 1;
                        if (importedFrames != metadata.FrameCount)
                        {
                            failures.Add($"frame count mismatch on {clip.name}: {importedFrames}.");
                        }

                        if (!Mathf.Approximately(clip.firstFrame, metadata.ClipFrameStart) ||
                            !Mathf.Approximately(clip.lastFrame, metadata.ClipFrameEnd))
                        {
                            failures.Add(
                                $"bind-pose prefix was not excluded on {clip.name}: " +
                                $"{clip.firstFrame:F3}-{clip.lastFrame:F3}.");
                        }
                    }
                }
            }

            var clipNames = new List<string>();
            var clipLengths = new List<float>();
            int nonConstantPoseCurveCount = 0;
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                {
                    clipNames.Add(clip.name);
                    clipLengths.Add(clip.length);
                    nonConstantPoseCurveCount += CountNonConstantPoseCurves(clip);
                    float expectedLength = (metadata.FrameCount - 1f) / metadata.FrameRate;
                    if (Mathf.Abs(clip.length - expectedLength) > 1f / metadata.FrameRate + 0.001f)
                    {
                        failures.Add($"clip length mismatch on {clip.name}: {clip.length:F6}.");
                    }
                }
            }

            if (clipNames.Count == 0)
            {
                failures.Add("No imported AnimationClip sub-asset.");
            }

            if (metadata.PoseAnimatedBoneCount <= 0 || metadata.MinimumNonConstantPoseCurves <= 0)
            {
                failures.Add("Pose-motion evidence is missing from the import contract.");
            }
            else if (nonConstantPoseCurveCount < metadata.MinimumNonConstantPoseCurves)
            {
                failures.Add(
                    $"pose motion collapsed: expected at least {metadata.MinimumNonConstantPoseCurves} " +
                    $"non-constant curves, imported {nonConstantPoseCurveCount}.");
            }

            string[] actualLabels = AssetDatabase.GetLabels(AssetDatabase.LoadMainAssetAtPath(assetPath));
            foreach (string expectedLabel in metadata.Labels ?? Array.Empty<string>())
            {
                if (Array.IndexOf(actualLabels, expectedLabel) < 0)
                {
                    failures.Add($"label missing: {expectedLabel}");
                }
            }

            return new ImportResult
            {
                LibraryID = metadata.LibraryID,
                AnimationID = metadata.AnimationID,
                AssetPath = assetPath,
                AssetGuid = guid,
                FBXSHA256 = metadata.FBXSHA256,
                AnimationType = importer != null ? importer.animationType.ToString() : string.Empty,
                AvatarSetup = importer != null ? importer.avatarSetup.ToString() : string.Empty,
                SourceAvatarPath = importer != null ? AssetDatabase.GetAssetPath(importer.sourceAvatar) : string.Empty,
                TargetAvatarSHA256 = metadata.TargetAvatarSHA256,
                RootMotionStatus = metadata.RootMotionStatus,
                ExpectedFrameCount = metadata.FrameCount,
                ImportedClipCount = clipNames.Count,
                ImportedClipNames = clipNames.ToArray(),
                ImportedClipLengths = clipLengths.ToArray(),
                ExpectedAnimatedBoneCount = metadata.PoseAnimatedBoneCount,
                MinimumNonConstantPoseCurves = metadata.MinimumNonConstantPoseCurves,
                NonConstantPoseCurveCount = nonConstantPoseCurveCount,
                PoseMotionValidation = nonConstantPoseCurveCount >= metadata.MinimumNonConstantPoseCurves
                    ? "PASS"
                    : "FAIL",
                Labels = actualLabels,
                Status = failures.Count == 0 ? "PASS" : "FAIL",
                Failures = failures.ToArray(),
            };
        }

        private static int CountNonConstantPoseCurves(AnimationClip clip)
        {
            int count = 0;
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            foreach (EditorCurveBinding binding in bindings)
            {
                string propertyName = binding.propertyName ?? string.Empty;
                if (propertyName.StartsWith("RootT", StringComparison.Ordinal) ||
                    propertyName.StartsWith("RootQ", StringComparison.Ordinal) ||
                    propertyName.StartsWith("MotionT", StringComparison.Ordinal) ||
                    propertyName.StartsWith("MotionQ", StringComparison.Ordinal))
                {
                    continue;
                }

                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null || curve.length < 2)
                {
                    continue;
                }

                float minimum = float.PositiveInfinity;
                float maximum = float.NegativeInfinity;
                foreach (Keyframe key in curve.keys)
                {
                    minimum = Mathf.Min(minimum, key.value);
                    maximum = Mathf.Max(maximum, key.value);
                }

                if (maximum - minimum > 0.0001f)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
