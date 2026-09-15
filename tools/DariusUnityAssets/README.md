# Darius native Unity model pipeline

This toolchain replaces the runtime GLB/JSON animation interpreter with ordinary Unity assets.
The repository remains source-only: the resulting `assets/models/darius_models.bundle` is a local
binary asset and stays ignored by Git, just like the current GLB files.

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

- the four existing local GLBs under `assets/models/`;
- Blender with the built-in glTF importer and FBX exporter;
- a Unity 6000.0.x editor compatible with the current Shape of Dreams build.

Run from PowerShell:

```powershell
pwsh tools/DariusUnityAssets/Build-NativeModels.ps1 `
  -UnityExe "C:\Program Files\Unity\Hub\Editor\6000.0.x\Editor\Unity.exe" `
  -BlenderExe "C:\Program Files\Blender Foundation\Blender\blender.exe"
```

The script creates a disposable Unity project under the system temp directory, converts the four
GLBs to FBX, imports them with Unity, builds the four skin prefabs, and writes:

```
assets/models/darius_models.bundle
```

Use `-KeepTemp` when inspecting imported clips, materials, bounds, or generated prefabs.

## Asset contract

The bundle contains these prefab names:

- `darius_classic.prefab`
- `darius_godking.prefab`
- `darius_dunkmaster.prefab`
- `darius_mecha.prefab`

Each prefab contains an `Animator` whose layer-0 states are named exactly after the imported Riot
animation clips. Idle/run clips are imported as loops; action/death clips remain one-shots.
Renderers use `updateWhenOffscreen=false` and conservative authored bounds.

God-King materials containing `Wolf_Mat` or `Throne` are split at build time into a separate
`DariusHidden_*` skinned renderer. That renderer starts disabled and is enabled only when the R
presentation asks for the authored hidden geometry. This avoids continuously skinning alpha-zero
wolf/throne meshes.

## Runtime fallback and cutover

`DariusNativeModelAssets` always prefers the bundle. If the bundle is absent, the current runtime
GLB implementation remains available as a temporary compatibility fallback, with performance guards
that cap its high-refresh sampling cost and disable off-screen skinning.

After the native bundle has passed all four-skin lobby and in-game smoke tests, the intended final
cleanup is to remove the fallback runtime model/animation stack:

- `DariusGlbRuntimeModel.cs`;
- the Darius use of `UniversalAnimation/*`;
- legacy animation-suppression Harmony guards;
- raw GLB and `.sodanim.json` files from the published package.

Do not delete the fallback before the native binary asset has been produced and smoke-tested; CI has
no access to the ignored model assets and cannot validate visual/animation fidelity by itself.
