[CmdletBinding()]
param(
    [string]$ProjectRoot,
    [string]$BlenderPath = 'D:\Games\SteamGames\steamapps\common\Blender\blender.exe',
    [switch]$FinalizeOnly
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (& git -C $PSScriptRoot rev-parse --show-toplevel).Trim()
}

$ProjectRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
$referenceRoot = Join-Path $ProjectRoot 'Reference/FrankAnim'
$assetRoot = Join-Path $ProjectRoot 'Assets/_Game/Animations/Frank'
$workingRoot = Join-Path $ProjectRoot 'Library/ProjectSAnimationImport'
$toolRoot = Join-Path $workingRoot 'Tools'
$extractRoot = Join-Path $workingRoot 'Extracted'
$logRoot = Join-Path $workingRoot 'Logs'
$manifestPath = Join-Path $workingRoot 'frank-blender-manifest.json'
$conversionReportPath = Join-Path $workingRoot 'frank-conversion-report.json'
$sourceIntegrityReportPath = Join-Path $assetRoot 'FrankSourceKeyAuditReport.json'
$converterPath = Join-Path $ProjectRoot 'Assets/_App/Core/Editor/Animation/FrankImport/convert_psa_to_fbx.py'

foreach ($requiredPath in @($referenceRoot, $assetRoot, $converterPath, $BlenderPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required Frank import path does not exist: $requiredPath"
    }
}

New-Item -ItemType Directory -Force -Path $toolRoot, $extractRoot, $logRoot | Out-Null

$umodelCommit = 'a0bfb468d42be831b126632fd8a0ae6b3614f981'
$umodelSha256 = '13502E5A4D8F6B5F32252AFEBD6360F7302CCFACCF6B8DDA65BEFF0BE2D364A0'
$addonCommit = '315bb2071d4ee084f35dddc9e515311da65953f1'
$addonSha256 = 'C92D8D78B352BD68BDBF7687C4A2FFEB2936AE43AEE51A24478C6454CAB62BE0'
$umodelDirectory = Join-Path $toolRoot 'UModel'
$umodelPath = Join-Path $umodelDirectory 'umodel.exe'
$addonDirectory = Join-Path $toolRoot 'io_scene_psk_psa'
$addonArchive = Join-Path $addonDirectory 'addon.zip'
$addonSourceRoot = Join-Path $addonDirectory 'src'
$addonPythonRoot = Join-Path $addonDirectory 'python'

function Assert-Hash([string]$Path, [string]$ExpectedHash) {
    $actualHash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    if ($actualHash -ne $ExpectedHash) {
        throw "Hash mismatch for $Path. Expected $ExpectedHash, got $actualHash."
    }
}

if (-not (Test-Path -LiteralPath $umodelPath)) {
    New-Item -ItemType Directory -Force -Path $umodelDirectory | Out-Null
    Invoke-WebRequest `
        -Headers @{ 'User-Agent' = 'ProjectS-Frank-Animation-Importer' } `
        -Uri "https://raw.githubusercontent.com/gildor2/UEViewer/$umodelCommit/umodel.exe" `
        -OutFile $umodelPath
}
Assert-Hash $umodelPath $umodelSha256

if (-not (Test-Path -LiteralPath $addonArchive)) {
    New-Item -ItemType Directory -Force -Path $addonDirectory | Out-Null
    Invoke-WebRequest `
        -Headers @{ 'User-Agent' = 'ProjectS-Frank-Animation-Importer' } `
        -Uri "https://codeload.github.com/DarklightGames/io_scene_psk_psa/zip/$addonCommit" `
        -OutFile $addonArchive
}
Assert-Hash $addonArchive $addonSha256

if (-not (Test-Path -LiteralPath $addonSourceRoot)) {
    Expand-Archive -LiteralPath $addonArchive -DestinationPath $addonSourceRoot -Force
}

$addonRepositoryRoot = Get-ChildItem -LiteralPath $addonSourceRoot -Directory |
    Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'io_scene_psk_psa/blender_manifest.toml') } |
    Select-Object -First 1
if ($null -eq $addonRepositoryRoot) {
    throw "Unable to locate the pinned io_scene_psk_psa source under $addonSourceRoot."
}

$wheelPath = Join-Path $addonRepositoryRoot.FullName 'io_scene_psk_psa/wheels/psk_psa_py-0.0.4-py3-none-any.whl'
if (-not (Test-Path -LiteralPath (Join-Path $addonPythonRoot 'psk_psa_py'))) {
    New-Item -ItemType Directory -Force -Path $addonPythonRoot | Out-Null
    $wheelArchive = Join-Path $addonPythonRoot 'psk_psa_py.zip'
    Copy-Item -LiteralPath $wheelPath -Destination $wheelArchive -Force
    Expand-Archive -LiteralPath $wheelArchive -DestinationPath $addonPythonRoot -Force
}

$sourceSets = @(
    @{ Pack = 'DualSwords'; FlatOutput = $true; Directories = @('Frank_Dual_Sword/Animations/Frank_Dual_In_place', 'Frank_Dual_Sword/Animations/Frank_Dual_Rootmotion') },
    @{ Pack = 'TwoHandedAxe'; Directories = @('Frank_2Handed_AXE/Animations') },
    @{ Pack = 'TwoHandedGreatSword'; Directories = @('Frank_2Handed_GreatSword/Animations/IP+RM') },
    @{ Pack = 'TwoHandedKatana'; Directories = @('Frank_2Handed_Katana/Animations/Frank_Katana_IP', 'Frank_2Handed_Katana/Animations/Frank_Katana_RM') },
    @{ Pack = 'TwoHandedSpear'; Directories = @('Frank_2Handed_Spear/Animations/Frank_Spear_IP', 'Frank_2Handed_Spear/Animations/Frank_Spear_RM') },
    @{ Pack = 'AssassinNinja'; Directories = @('Frank_Assassin_Ninja/Animations/Frank_Assassin_IP', 'Frank_Assassin_Ninja/Animations/Frank_Assassin_RM') },
    @{ Pack = 'SwordTwoFemale'; Directories = @('Frank_Sword2_Female/Animations/01_Mannequin_Female') },
    @{ Pack = 'Warrior'; Directories = @('Frank_Warrior/Animations/Frank_Warrior_IP', 'Frank_Warrior/Animations/Frank_Warrior_RM') },
    @{ Pack = 'WhipFemale'; Directories = @(
        'Frank_Whip_Female/Animations/01_Attack_RootMotion',
        'Frank_Whip_Female/Animations/02_Combo_Set_RootMotion',
        'Frank_Whip_Female/Animations/03_Skill',
        'Frank_Whip_Female/Animations/04_Run_Walk_InPlace',
        'Frank_Whip_Female/Animations/05_Jump',
        'Frank_Whip_Female/Animations/06_Hit',
        'Frank_Whip_Female/Animations/07_KO',
        'Frank_Whip_Female/Animations/08_ETC'
    ) },
    @{ Pack = 'DamageReactions'; Directories = @(
        'Frank_Damages/Animations/01_Normal',
        'Frank_Damages/Animations/02_Strong',
        'Frank_Damages/Animations/03_KnockDown',
        'Frank_Damages/Animations/04_Stun',
        'Frank_Damages/Animations/05_Wall',
        'Frank_Damages/Animations/06_Air',
        'Frank_Damages/Animations/07_KnockOut',
        'Frank_Damages/Animations/08_Parrying_React',
        'Frank_Damages/Animations/09_Bound',
        'Frank_Damages/Animations/10_Getup',
        'Frank_Damages/Animations/11_Critical_Attack_Set',
        'Frank_Damages/Animations/12_Sting',
        'Frank_Damages/Animations/13_Electric_Shock',
        'Frank_Damages/Animations/in_Place_Anims'
    ) },
    @{ Pack = 'StealthKills'; Directories = @(
        'Frank_Stealth_Kill/Animations/Stealth_Kill',
        'Frank_Stealth_Kill/Animations/Stealth_Kill_Hit',
        'Frank_Stealth_Kill/Animations/Stealth_Move'
    ) }
)

if (-not $FinalizeOnly) {
Write-Host 'Extracting selected Frank AnimSequence packages with UE Viewer 1590...'
foreach ($sourceSet in $sourceSets) {
    foreach ($sourceDirectory in $sourceSet.Directories) {
        $physicalDirectory = Join-Path $referenceRoot $sourceDirectory
        if (-not (Test-Path -LiteralPath $physicalDirectory)) {
            throw "Configured Frank source directory is missing: $physicalDirectory"
        }

        $safeName = ($sourceDirectory -replace '[^A-Za-z0-9_-]', '_')
        $logPath = Join-Path $logRoot "umodel-$safeName.log"
        $arguments = @(
            '-game=ue4.23',
            "-path=$referenceRoot",
            '-export',
            "-out=$extractRoot",
            '-notex',
            '-nomesh',
            '-nostat',
            "$sourceDirectory/*.uasset"
        )
        $output = & $umodelPath @arguments 2>&1
        $exitCode = $LASTEXITCODE
        $output | Set-Content -LiteralPath $logPath -Encoding utf8
        if ($exitCode -ne 0) {
            throw "UE Viewer failed for $sourceDirectory (exit $exitCode). See $logPath."
        }
    }
    Write-Host "  extracted $($sourceSet.Pack)"
}

function Get-MotionMode([string]$RelativePsaPath, [string]$ClipName) {
    if ($RelativePsaPath -match '(?i)Frank_(Dual_In_place|Katana_IP|Spear_IP|Assassin_IP|Warrior_IP)|in_Place_Anims|Run_Walk_InPlace') {
        return 'InPlace'
    }
    if ($RelativePsaPath -match '(?i)Frank_(Dual_Rootmotion|Katana_RM|Spear_RM|Assassin_RM|Warrior_RM)|RootMotion') {
        return 'RootMotion'
    }
    if ($ClipName -match '(?i)_IP$|_In_?Place$') {
        return 'InPlace'
    }
    if ($ClipName -match '(?i)(8Way|4Way)_(Run|Walk)|Unequip_(Run|Walk)|Equip_Run|(^|_)Idle($|_)') {
        return 'InPlace'
    }
    return 'RootMotion'
}

function Get-ActionCategory([string]$ClipName, [string]$RelativePsaPath) {
    if ($RelativePsaPath -match '(?i)Stealth_Kill_Hit') { return 'Victim' }
    if ($RelativePsaPath -match '(?i)Stealth_Move') { return 'Movement' }
    if ($RelativePsaPath -match '(?i)Stealth_Kill') { return 'Attacker' }
    if ($ClipName -match '(?i)Attack|Combo|Critical') { return 'Attacks' }
    if ($ClipName -match '(?i)Skill') { return 'Skills' }
    if ($ClipName -match '(?i)Evade|Step|Tumbling|Dash|Sliding') { return 'Evasion' }
    if ($ClipName -match '(?i)Block|Guard|Parry') { return 'Defense' }
    if ($ClipName -match '(?i)Hit|Damage|Stun|Knock|Bound|Wall|Electric|Sting') { return 'Reactions' }
    if ($ClipName -match '(?i)Die|Death|KnockOut|(^|_)KO($|_)') { return 'Death' }
    if ($ClipName -match '(?i)Getup|Recover') { return 'Recovery' }
    if ($ClipName -match '(?i)Equip|Unequip|Draw|Putup') { return 'Equipment' }
    if ($ClipName -match '(?i)Jump|Fall|Air') { return 'Air' }
    if ($ClipName -match '(?i)Idle|Walk|Run|Sprint|Turn') { return 'Locomotion' }
    return 'Other'
}

$jobs = [System.Collections.Generic.List[object]]::new()
$outputKeys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($sourceSet in $sourceSets) {
    foreach ($sourceDirectory in $sourceSet.Directories) {
        $exportDirectory = Join-Path $extractRoot $sourceDirectory
        if (-not (Test-Path -LiteralPath $exportDirectory)) {
            continue
        }

        foreach ($psa in Get-ChildItem -LiteralPath $exportDirectory -File -Filter '*.psa') {
            $relativePsaPath = $psa.FullName.Substring($extractRoot.Length + 1).Replace('\', '/')
            $clipName = $psa.BaseName
            $motion = Get-MotionMode $relativePsaPath $clipName
            $category = Get-ActionCategory $clipName $relativePsaPath
            $relativeAssetPath = if ($sourceSet.FlatOutput) {
                "Assets/_Game/Animations/Frank/$($sourceSet.Pack)/Animations/$motion/$clipName.fbx"
            } else {
                "Assets/_Game/Animations/Frank/$($sourceSet.Pack)/Animations/$motion/$category/$clipName.fbx"
            }
            if (-not $outputKeys.Add($relativeAssetPath)) {
                throw "Two Frank source animations map to the same output: $relativeAssetPath"
            }

            $jobs.Add([ordered]@{
                pack = $sourceSet.Pack
                sourceAsset = ($relativePsaPath -replace '\.psa$', '.uasset')
                sourceGroup = Split-Path -Parent $relativePsaPath
                motion = $motion
                category = $category
                assetPath = $relativeAssetPath
                psa = $psa.FullName.Replace('\', '/')
                output = (Join-Path $ProjectRoot $relativeAssetPath).Replace('\', '/')
                overwrite = $true
            })
        }
    }
}

if ($jobs.Count -eq 0) {
    throw 'UE Viewer produced no PSA animations for the selected Frank source sets.'
}

$manifest = [ordered]@{
    rigOutputRoot = (Join-Path $assetRoot 'Rig/Source').Replace('\', '/')
    rigAssetRoot = 'Assets/_Game/Animations/Frank/Rig/Source'
    addonRoot = $addonRepositoryRoot.FullName.Replace('\', '/')
    pythonRoot = $addonPythonRoot.Replace('\', '/')
    report = $conversionReportPath.Replace('\', '/')
    jobs = $jobs
}
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding utf8

Write-Host "Converting $($jobs.Count) PSA sequences to FBX with Blender..."
if (Test-Path -LiteralPath $conversionReportPath) {
    Remove-Item -LiteralPath $conversionReportPath -Force
}
& $BlenderPath `
    --background `
    --factory-startup `
    --python $converterPath `
    -- `
    --manifest $manifestPath 2>&1 |
    Tee-Object -FilePath (Join-Path $logRoot 'blender-conversion.log')
$blenderExitCode = $LASTEXITCODE
}

if (-not (Test-Path -LiteralPath $conversionReportPath)) {
    throw "Blender did not produce a conversion report. See $(Join-Path $logRoot 'blender-conversion.log')."
}
$conversionReport = Get-Content -LiteralPath $conversionReportPath -Raw | ConvertFrom-Json
$failed = @($conversionReport | Where-Object status -eq 'failed')
if (-not $FinalizeOnly -and $failed.Count -gt 0) {
    $retryReportPath = Join-Path $workingRoot 'frank-conversion-retry-report.json'
    $retryArguments = [System.Collections.Generic.List[string]]::new()
    foreach ($argument in @(
        '--background',
        '--factory-startup',
        '--python', $converterPath,
        '--',
        '--manifest', $manifestPath,
        '--report', $retryReportPath
    )) {
        $retryArguments.Add($argument)
    }
    foreach ($failedClip in $failed) {
        $retryArguments.Add('--asset-path')
        $retryArguments.Add($failedClip.assetPath)
    }

    Write-Host "Retrying $($failed.Count) failed Frank FBX exports..."
    if (Test-Path -LiteralPath $retryReportPath) {
        Remove-Item -LiteralPath $retryReportPath -Force
    }
    & $BlenderPath @retryArguments 2>&1 |
        Tee-Object -FilePath (Join-Path $logRoot 'blender-conversion-retry.log')
    if (-not (Test-Path -LiteralPath $retryReportPath)) {
        throw "Blender did not produce a retry report. See $(Join-Path $logRoot 'blender-conversion-retry.log')."
    }
    $retryReport = @(Get-Content -LiteralPath $retryReportPath -Raw | ConvertFrom-Json)
    $retryByAssetPath = @{}
    foreach ($retryRecord in $retryReport) {
        $retryByAssetPath[$retryRecord.assetPath] = $retryRecord
    }
    $conversionReport = @($conversionReport | ForEach-Object {
        if ($retryByAssetPath.ContainsKey($_.assetPath)) {
            $retryByAssetPath[$_.assetPath]
        } else {
            $_
        }
    })
    $conversionReport | ConvertTo-Json -Depth 12 |
        Set-Content -LiteralPath $conversionReportPath -Encoding utf8
    $failed = @($conversionReport | Where-Object status -eq 'failed')
}
if (-not $FinalizeOnly -and $blenderExitCode -ne 0 -and $failed.Count -eq 0) {
    Write-Warning 'The full Blender pass reported a failure, but every failed export succeeded on targeted retry.'
}
if ($failed.Count -gt 0) {
    throw "$($failed.Count) Frank animations failed conversion. See $conversionReportPath."
}

$sourceIntegrityAudit = [ordered]@{
    schemaVersion = 4
    scannedClipCount = $conversionReport.Count
    skeletonCount = @($conversionReport.skeletonFingerprint | Sort-Object -Unique).Count
    sourceTransformMutationCount = ($conversionReport | Measure-Object -Property sourceTransformMutationCount -Sum).Sum
    maximumBindPoseRotationErrorDegrees = ($conversionReport | Measure-Object -Property bindPoseMaxRotationErrorDegrees -Maximum).Maximum
    maximumBindPoseLocationError = ($conversionReport | Measure-Object -Property bindPoseMaxLocationError -Maximum).Maximum
    quaternionHemisphereFlipCount = ($conversionReport | Measure-Object -Property quaternionHemisphereFlipCount -Sum).Sum
    sourceEngine = 'Unreal Engine 4.23'
    calfRollCorrectionAxis = 'localX'
    calfRollCorrectionDegrees = 180
    policy = 'Every clip is rebuilt on its embedded source bind pose. Frame zero is reserved for Avatar matching and excluded from the Unity clip. Equivalent q/-q quaternion notation is normalized, and both Unreal calf rotations receive a deterministic 180-degree local-X basis conversion before Unity Humanoid retargeting. No positions are clamped, smoothed or interpolated.'
}
$sourceIntegrityAudit | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $sourceIntegrityReportPath -Encoding utf8

foreach ($pack in $sourceSets.Pack) {
    $packItems = @($conversionReport | Where-Object pack -eq $pack | ForEach-Object {
        [ordered]@{
            name = $_.sequence
            assetPath = $_.assetPath
            sourceAsset = $_.sourceAsset
            sourceGroup = $_.sourceGroup
            motion = $_.motion
            category = $_.category
            sourceFrames = $_.sourceFrames
            frames = $_.frames
            fps = $_.fps
            sourceFps = $_.sourceFps
            durationSeconds = $_.durationSeconds
            resampledToTargetFps = $_.resampledToTargetFps
            sourceBones = $_.sourceBones
            bodyBones = $_.bodyBones
            mappedBones = $_.mappedBones
            skeletonFingerprint = $_.skeletonFingerprint
            rigAssetPath = $_.rigAssetPath
            bindPoseMaxRotationErrorDegrees = $_.bindPoseMaxRotationErrorDegrees
            bindPoseMaxLocationError = $_.bindPoseMaxLocationError
            sourceTransformMutationCount = $_.sourceTransformMutationCount
            referencePoseFrames = $_.referencePoseFrames
            authoredAnimationStartFrame = $_.authoredAnimationStartFrame
            authoredAnimationEndFrame = $_.authoredAnimationEndFrame
            calfRollCorrectionAxis = $_.calfRollCorrectionAxis
            calfRollCorrectionDegrees = $_.calfRollCorrectionDegrees
            quaternionHemisphereFlipCount = $_.quaternionHemisphereFlipCount
            bytes = $_.bytes
            warnings = $_.warnings
        }
    })
    $catalog = [ordered]@{
        schemaVersion = 4
        pack = $pack
        rigStrategy = 'PerSourceBindPose'
        animationType = 'Humanoid'
        clipCount = $packItems.Count
        clips = $packItems
    }
    $packRoot = Join-Path $assetRoot $pack
    New-Item -ItemType Directory -Force -Path $packRoot | Out-Null
    $catalogFileName = if ($pack -eq 'DualSwords') {
        'FrankDualSwordAnimationCatalog.json'
    } else {
        "Frank$($pack)AnimationCatalog.json"
    }
    $catalogPath = Join-Path $packRoot $catalogFileName
    $catalog | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $catalogPath -Encoding utf8
}

Write-Host "Frank conversion complete: $($conversionReport.Count) Humanoid source FBXs generated."
