using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace _App.Editor.Animation
{
    internal static class CuratedAnimationClipBuilder
    {
        private const string MenuPath = "Tools/Animation/Rebuild Curated Clips";
        private const string Shared = "Assets/_Game/Animations/Character/Shared/";
        private const string Legacy = "Assets/_Game/Animations/Character/2. Basic/";
        private const string Attack =
            "Assets/_Game/Animations/Clazy/Animations/P4_CLazyAttack/";
        private const string Weapons =
            "Assets/_Game/Animations/Character/Weapons/";

        private readonly struct CuratedClip
        {
            public string SourceAsset { get; }
            public string SourceClip { get; }
            public string TargetPath { get; }
            public bool Loop { get; }

            public CuratedClip(
                string sourceAsset,
                string sourceClip,
                string targetPath,
                bool loop)
            {
                SourceAsset = sourceAsset;
                SourceClip = sourceClip;
                TargetPath = targetPath;
                Loop = loop;
            }
        }

        private static readonly CuratedClip[] Manifest =
        {
            new CuratedClip(
                Legacy + "Jump/Jumping/JumpIdle.fbx",
                "JumpIdle",
                Shared + "Jump/Jump_Rise_Idle.anim",
                false),
            new CuratedClip(
                Legacy + "Jump/Jumping/JumpWalking.fbx",
                "JumpWalking",
                Shared + "Jump/Jump_Rise_Walk.anim",
                false),
            new CuratedClip(
                Legacy + "Jump/Jumping/JumpRunning.fbx",
                "JumpRunning",
                Shared + "Jump/Jump_Rise_Run.anim",
                false),
            new CuratedClip(
                Legacy + "Jump/Jumping/JumpSprinting.fbx",
                "JumpSprinting",
                Shared + "Jump/Jump_Rise_Sprint.anim",
                false),
            new CuratedClip(
                Legacy + "Jump/Falling/FallShort.fbx",
                "FallShort",
                Shared + "Jump/Jump_Fall_Short.anim",
                true),
            new CuratedClip(
                Legacy + "Jump/Falling/FallLarge.fbx",
                "FallLarge",
                Shared + "Jump/Jump_Fall_Long.anim",
                true),
            new CuratedClip(
                Legacy + "Jump/Jumping/JumpIdle.fbx",
                "LandDefault",
                Shared + "Jump/Jump_Land_Idle.anim",
                false),
            new CuratedClip(
                Legacy + "Jump/Jumping/JumpWalking.fbx",
                "LandWalking",
                Shared + "Jump/Jump_Land_Walk.anim",
                false),
            new CuratedClip(
                Legacy + "Jump/Jumping/JumpRunning.fbx",
                "LandRunning",
                Shared + "Jump/Jump_Land_Run.anim",
                false),
            new CuratedClip(
                Legacy + "Jump/Jumping/JumpSprinting.fbx",
                "LandSprinting",
                Shared + "Jump/Jump_Land_Sprint.anim",
                false),
            new CuratedClip(
                Legacy + "Jump/Landing/LandSoft.fbx",
                "LandSoft",
                Shared + "Jump/Jump_Land_Soft.anim",
                false),
            new CuratedClip(
                Legacy + "Jump/Landing/LandMedium.fbx",
                "LandMedium",
                Shared + "Jump/Jump_Land_Medium.anim",
                false),
            new CuratedClip(
                Legacy + "Jump/Landing/LandHard.fbx",
                "LandHard",
                Shared + "Jump/Jump_Land_Hard.anim",
                false),
            new CuratedClip(
                Legacy + "Jump/AirDash/CLazy@Esc_BoostDash_Air_Front.FBX",
                "AirDashFront",
                Shared + "Jump/Jump_AirDash_F.anim",
                false),
            new CuratedClip(
                Legacy + "Jump/AirDash/AirDashBack.FBX",
                "AirDashBack",
                Shared + "Jump/Jump_AirDash_B.anim",
                false),
            new CuratedClip(
                Attack + "Attack_Punch_Run/CLazy@Attack_Hand_PwR.FBX",
                "Attack_Hand_PwR",
                Weapons + "Unarmed/Ground/Unarmed_Special.anim",
                false),
            new CuratedClip(
                Attack + "Attack_Punch_Run/CLazy@Attack_Hand_Ready.FBX",
                "Attack_Hand_Ready",
                Weapons + "Unarmed/Defense/Unarmed_Guard.anim",
                false)
        };

        [MenuItem(MenuPath)]
        private static void Rebuild()
        {
            var report = new StringBuilder();
            int created = 0;
            int refreshed = 0;
            int failed = 0;

            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < Manifest.Length; i++)
                {
                    CuratedClip entry = Manifest[i];
                    if (!TryLoadSource(entry, out AnimationClip source, out string error))
                    {
                        failed++;
                        report.AppendLine($"  FAIL {entry.TargetPath}: {error}");
                        continue;
                    }

                    bool existed = Build(entry, source);
                    if (existed)
                    {
                        refreshed++;
                    }
                    else
                    {
                        created++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            report.Insert(
                0,
                $"[Curated Clips] created {created}, refreshed {refreshed}, failed {failed}\n");
            if (failed > 0)
            {
                Debug.LogError(report.ToString());
            }
            else
            {
                Debug.Log(report.ToString());
            }
        }

        private static bool TryLoadSource(
            CuratedClip entry,
            out AnimationClip source,
            out string error)
        {
            source = null;
            UnityEngine.Object[] assets =
                AssetDatabase.LoadAllAssetsAtPath(entry.SourceAsset);
            if (assets == null || assets.Length == 0)
            {
                error = $"source asset missing ({entry.SourceAsset})";
                return false;
            }

            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is AnimationClip clip &&
                    string.Equals(clip.name, entry.SourceClip, StringComparison.Ordinal))
                {
                    source = clip;
                    error = null;
                    return true;
                }
            }

            error = $"clip '{entry.SourceClip}' not found in source";
            return false;
        }

        private static bool Build(CuratedClip entry, AnimationClip source)
        {
            string name = Path.GetFileNameWithoutExtension(entry.TargetPath);
            AnimationClip target =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(entry.TargetPath);
            bool existed = target != null;
            if (existed)
            {
                EditorUtility.CopySerialized(source, target);
            }
            else
            {
                string directory = Path.GetDirectoryName(entry.TargetPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                target = UnityEngine.Object.Instantiate(source);
                AssetDatabase.CreateAsset(target, entry.TargetPath);
            }

            target.name = name;
            AnimationClipSettings settings =
                AnimationUtility.GetAnimationClipSettings(target);
            settings.loopTime = entry.Loop;
            AnimationUtility.SetAnimationClipSettings(target, settings);
            EditorUtility.SetDirty(target);
            return existed;
        }

        [MenuItem("Tools/Animation/Report Curated Clip Sources")]
        private static void ReportSources()
        {
            var report = new StringBuilder();
            report.AppendLine("target;source;clip;length;loop;bindings");
            for (int i = 0; i < Manifest.Length; i++)
            {
                CuratedClip entry = Manifest[i];
                AnimationClip target =
                    AssetDatabase.LoadAssetAtPath<AnimationClip>(entry.TargetPath);
                string length = target != null
                    ? target.length.ToString("F3", CultureInfo.InvariantCulture)
                    : "missing";
                string bindings = target != null
                    ? AnimationUtility.GetCurveBindings(target).Length
                        .ToString(CultureInfo.InvariantCulture)
                    : "-";
                report.AppendLine(string.Join(";", new[]
                {
                    Path.GetFileNameWithoutExtension(entry.TargetPath),
                    Path.GetFileName(entry.SourceAsset),
                    entry.SourceClip,
                    length,
                    entry.Loop ? "yes" : "no",
                    bindings
                }));
            }

            Debug.Log(report.ToString());
        }
    }
}