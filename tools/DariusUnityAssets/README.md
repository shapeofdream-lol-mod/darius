# Darius native Unity model pipeline

This toolchain replaces the runtime GLB/JSON animation interpreter with ordinary Unity assets.
The repository remains source-only: the resulting `assets/models/darius_models.bundle` and its
`.fingerprint` stamp are local generated assets and stay ignored by Git, like the current raw GLB
inputs.

## Why this exists

Shape of Dreams already uses Unity `Animator`, `EntityModel`, and `EntityAnimation`. Parsing GLB,
sampling every animation track in C#, rewriting every bone transform every rendered frame, and
forcing `SkinnedMeshRenderer.updateWhenOffscreen` duplicates engine/game work and scales badly on
high-refresh systems.

The native pipeline moves expensive conversion to build time:

```
Riot-derived local GLB
    -> Blender headless GLB -> FBX conversion
    -> Unity 6000.0.x native FBX importer
    -> Unity prefab + AnimatorController + AnimationClip assets
    -> LZ4 AssetBundle
    -> assets/models/darius_models.bundle
```

The shipped mod then uses `AssetBundle.LoadFromFile`, Unity `Animator`, normal skinned-mesh culling,
and `EntityAnimation.SetupModel()`. Blender and the importer are not runtime dependencies.

## Build

Requirements:

- PowerShell 7 (`pwsh`); `PackageMod`/`DeployMod` run the native-bundle freshness check from
  `src/DariusPrototype/Directory.Build.targets`;
- the four existing local GLBs under `assets/models/`;
- Blender with the built-in glTF importer and FBX exporter;
- a Unity 6000.0.x editor compatible with the current Shape of Dreams build.

Run from PowerShell:

```powershell
pwsh tools/DariusUnityAssets/Build-NativeModels.ps1 `
  -UnityExe "C:\Program Files\Unity\Hub\Editor\6000.0.x\Editor\Unity.exe" `
  -BlenderExe "C:\Program Files\Blender Foundation\Blender\blender.exe"
```

The script creates an isolated workspace under:

```text
build/native-assets-work/<timestamp>_<id>/
```

Inside that workspace it converts the four GLBs to FBX, creates a disposable Unity project, imports
the generated FBX/textures/material maps, builds the four skin prefabs and overlay meshes, and writes:

```text
assets/models/darius_models.bundle
assets/models/darius_models.bundle.fingerprint
```

A successful build removes the workspace by default. `-KeepTemp` preserves it for inspection; failed
builds are also preserved so the Unity logs and generated project can be examined.

## Shared asset contract

`src/DariusPrototype/Native/DariusNativeAssetContract.cs` is copied into the temporary Unity project
and is shared by build-time generation and runtime loading. Do not duplicate its naming rules in an
Editor-only or runtime-only helper.

Generated prefab asset paths are:

```text
Assets/DariusGenerated/darius_classic.prefab
Assets/DariusGenerated/darius_godking.prefab
Assets/DariusGenerated/darius_dunkmaster.prefab
Assets/DariusGenerated/darius_mecha.prefab
```

Runtime loading uses these exact project-relative AssetBundle paths rather than scanning
`GetAllAssetNames()` or matching suffixes. Filtered overlay meshes follow the same shared contract:

```text
Assets/DariusGenerated/overlay_<variant>_<rendererOrdinal>_<materialHash>.asset
```

Each prefab contains an `Animator`. Layer-0 state names are derived from the imported Riot clip names
through `DariusNativeAssetContract.AnimatorStateName`: `.`, `/`, and `\` are normalized to `_`.
The Unity builder rejects state-name collisions after that normalization. Idle/run clips are imported
as loops; action/death clips remain one-shots. Renderers use `updateWhenOffscreen=false`.

Health/weapon anchor candidates also live in the shared asset contract so the Unity validator,
`EntityModel` setup, and runtime VFX/attachment lookup accept the same hierarchy names.

## Hidden God-King geometry

The Unity mesh processor separates authored hidden God-King geometry during prefab generation.
Toggleable wolf geometry is generated under `DariusHidden_Wolf_*`; permanently hidden geometry such
as the throne is generated under `DariusHidden_Static_*`. Runtime renderer classification uses these
shared object-name rules instead of guessing from mesh or material names.

Hidden presentation renderers start disabled and are excluded from `EntityModel.bodyRenderers` and
from overlay source ordinals. Only the wolf renderer is enabled when the R presentation requests the
authored wolf geometry. This avoids continuously skinning alpha-zero wolf/throne geometry and keeps
Editor/runtime overlay ordinals consistent.

## Fingerprint and packaging gate

The fingerprint is derived from the actual native-asset generation inputs, including:

- `DariusNativeSkinProfiles.cs`;
- `DariusNativeAssetContract.cs`;
- all Unity Editor pipeline `.cs` files under `tools/DariusUnityAssets/Editor/`;
- `convert_glb_to_fbx.py`;
- the build/fingerprint PowerShell scripts;
- the four raw GLB inputs.

`PackageMod` and `DeployMod` call `Test-NativeModelBundle.ps1`. Packaging fails when the bundle or
stamp is missing, or when the stored fingerprint no longer matches those generation inputs. A code
change that alters the shared asset contract therefore requires a fresh native bundle build.

## Runtime fallback and cutover

`DariusNativeModelAssets` always prefers the bundle. Raw GLB loading is only an optional local-development
fallback when the corresponding source file physically exists. Published packages do not ship the GLBs.

After the native bundle has passed all four-skin lobby and in-game smoke tests, the intended final
cleanup is to remove the fallback runtime model/animation stack in the branch that owns that cleanup,
including the legacy GLB interpreter and universal retarget fallback where no longer required.

Do not delete the fallback before the native binary asset has been produced and smoke-tested. CI has
no access to the ignored model assets and cannot validate visual/animation fidelity by itself.
