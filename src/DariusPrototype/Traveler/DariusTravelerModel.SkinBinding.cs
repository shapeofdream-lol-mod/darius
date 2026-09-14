public sealed class DariusSkinModelBinding : MonoBehaviour
{
    public string skinResourceName;
    public string modelFile;
    public string displayName;
    public string variantKey;
    public bool isGodKingSkin;
    public float modelScale;
    public float modelYOffset;
    public float modelYaw;
    public int expectedPrimitives, expectedBones, expectedAnimations;
    public string idleClip, idleVariantClip, runClip, deathClip;
    public string attack1Clip, attack2Clip, critClip;
    public string qIntroClip, qClip, wClip, eClip, rClip;
    public string attack1ToIdleClip, attack2ToIdleClip, critToIdleClip;
    public string eToRunClip, eToIdleClip, rToRunClip;
}

public sealed partial class DariusTravelerModelInstance : MonoBehaviour
{
    private void ValidateLoadedSkinProfile(DariusSkinModelBinding binding)
    {
        if (_model == null || binding == null) throw new InvalidOperationException("Darius skin profile validation received null model/binding.");
        if (binding.expectedPrimitives > 0 && _model.primitiveCount != binding.expectedPrimitives)
            throw new InvalidDataException("Skin " + binding.skinResourceName + " primitive mismatch expected=" + binding.expectedPrimitives + " actual=" + _model.primitiveCount);
        if (binding.expectedBones > 0 && _model.boneCount != binding.expectedBones)
            throw new InvalidDataException("Skin " + binding.skinResourceName + " bone mismatch expected=" + binding.expectedBones + " actual=" + _model.boneCount);
        if (binding.expectedAnimations > 0 && _model.animationCount != binding.expectedAnimations)
            throw new InvalidDataException("Skin " + binding.skinResourceName + " animation mismatch expected=" + binding.expectedAnimations + " actual=" + _model.animationCount);
        ValidateBoundClip("idleClip", binding.idleClip, true);
        ValidateBoundClip("runClip", binding.runClip, true);
        ValidateBoundClip("deathClip", binding.deathClip, true);
        ValidateBoundClip("attack1Clip", binding.attack1Clip, true);
        ValidateBoundClip("attack2Clip", binding.attack2Clip, true);
        ValidateBoundClip("critClip", binding.critClip, true);
        ValidateBoundClip("qIntroClip", binding.qIntroClip, true);
        ValidateBoundClip("qClip", binding.qClip, true);
        ValidateBoundClip("wClip", binding.wClip, true);
        ValidateBoundClip("eClip", binding.eClip, true);
        ValidateBoundClip("rClip", binding.rClip, true);
        ValidateBoundClip("attack1ToIdleClip", binding.attack1ToIdleClip, false);
        ValidateBoundClip("attack2ToIdleClip", binding.attack2ToIdleClip, false);
        ValidateBoundClip("critToIdleClip", binding.critToIdleClip, false);
        ValidateBoundClip("eToRunClip", binding.eToRunClip, false);
        ValidateBoundClip("eToIdleClip", binding.eToIdleClip, false);
        ValidateBoundClip("rToRunClip", binding.rToRunClip, false);
        DariusLog.Info("SKIN-PROFILE-ASSERT", "PASS skin=" + binding.skinResourceName + " profile=" + binding.variantKey +
            " primitives=" + _model.primitiveCount + " bones=" + _model.boneCount + " animations=" + _model.animationCount);
    }

    private void ValidateBoundClip(string slot, string clip, bool required)
    {
        if (string.IsNullOrEmpty(clip))
        {
            if (required) throw new InvalidDataException("Required skin animation binding is empty: " + slot);
            return;
        }
        if (!_model.HasClip(clip))
        {
            if (required) throw new InvalidDataException("Required skin animation is absent from GLB: " + slot + "=" + clip);
            DariusLog.Warn("SKIN-PROFILE-ASSERT", "Optional animation absent slot=" + slot + " clip=" + clip + " skin=" + (_binding != null ? _binding.variantKey : "<unknown>"));
        }
    }

    public void BindHero(Hero hero)
    {
        CancelAttackFacing("hero rebound");
        if (_facingPivot != null) _facingPivot.localRotation = Quaternion.identity;
        _hero = hero;
        if (_hero != null)
        {
            DariusVoiceRuntime.Ensure(_hero);
            _lastPosition = _hero.transform.position;
            _lastSample = Time.time;
            _isMoving = false;
            _wasMoving = false;
            _sampledSpeed = 0f;
            _recentMovementDirection = Vector3.zero;
            _recentMovementDirectionAt = -999f;
            _lastFacing = _hero.transform.forward;
            _lastFacing.y = 0f;
            if (_lastFacing.sqrMagnitude < 0.001f) _lastFacing = Vector3.forward;
            else _lastFacing.Normalize();
        }
    }

    private void OnEnable()
    {
        if (_model != null) return;
        try
        {
            Hero hostHero = GetComponentInParent<Hero>();
            _lobbyPreview = hostHero == null;
            DariusSkinModelBinding skinBinding = GetComponent<DariusSkinModelBinding>();
            if (skinBinding == null || string.IsNullOrEmpty(skinBinding.modelFile))
                throw new InvalidOperationException("Darius Skin resource has no explicit model binding: " + gameObject.name);
            _binding = skinBinding;
            _godKing = skinBinding.isGodKingSkin;
            string file = skinBinding.modelFile;
            DariusLog.Info("SKIN-MODEL-BIND", "Resolved skinResource=" + skinBinding.skinResourceName +
                " display=" + skinBinding.displayName + " modelFile=" + file + " profile=" + skinBinding.variantKey);
            string path = System.IO.Path.Combine(DariusMedia.Root, "assets", "models", file);
            _model = DariusGlbRuntimeModel.Load(gameObject, path);
            if (_model == null) throw new InvalidOperationException("GLB runtime loader returned null.");
            ValidateLoadedSkinProfile(skinBinding);

            float boundScale = skinBinding.modelScale > 0.00001f ? skinBinding.modelScale : NativeModelScale;
            float boundYOffset = Mathf.Abs(skinBinding.modelYOffset) > 0.00001f ? skinBinding.modelYOffset : NativeModelYOffset;

            GameObject facingGo = new GameObject("DariusVisualFacingPivot");
            _facingPivot = facingGo.transform;
            _facingPivot.SetParent(_model.root.transform.parent, false);
            _facingPivot.localPosition = Vector3.zero;
            _facingPivot.localRotation = Quaternion.identity;
            _facingPivot.localScale = Vector3.one;
            _model.root.transform.SetParent(_facingPivot, false);

            _model.root.transform.localPosition = new Vector3(0f, boundYOffset, 0f);
            _model.root.transform.localRotation = Quaternion.Euler(0f, _binding.modelYaw, 0f);
            DariusLog.Info("SKIN-FACING-CALIBRATION", "Applied authored root yaw=" + _binding.modelYaw.ToString("0.#") + " skin=" + _binding.variantKey + " model=" + file);
            _model.root.transform.localScale = Vector3.one * boundScale;

            // Complete the stock EntityModel renderer contract after the runtime GLB exists.
            // Older builds left bodyRenderers empty and then suppressed EntityVisual tail NREs;
            // populating the real renderers lets lobby/preview/highlight consumers see the same model.
            try
            {
                EntityModel entityModel = GetComponent<EntityModel>();
                if (entityModel != null)
                {
                    Renderer[] renderers = _model.GetEntityBodyRenderers();
                    entityModel.bodyRenderers = renderers ?? new Renderer[0];
                    DariusLog.Info("TRAVELER-MODEL", "EntityModel bodyRenderers rebound visibleOnly count=" + entityModel.bodyRenderers.Length +
                        " skin=" + VariantKey + "; authored-hidden animation props excluded from native hover/highlight.");
                }
            }
            catch (Exception rendererError)
            {
                DariusLog.Exception("TRAVELER-MODEL", rendererError, "Failed binding runtime GLB renderers into EntityModel contract");
            }

            BindHero(hostHero);
            _nextIdleVariantAt = Time.time + 7.5f;
            _nextPreviewShowcaseAt = Time.time + 24.0f;
            // The lobby display may recreate EntityModel several times while the user changes
            // selection/skin. Replaying an entry clip on every rebuild made the preview look as if
            // actions were constantly interrupting each other. Lobby starts from the stable idle;
            // the authored intro remains available in the actual in-game model.
            if (_lobbyPreview)
                _model.Play(IdleClip, true, true, 1f, 0f);
            else if (_godKing && _model.HasClip("Idle_In"))
                _animationSequence = StartCoroutine(PlayIntroSequence());
            else
                _model.Play(IdleClip, true, true);

            DariusLog.Info("TRAVELER-MODEL", "Darius EntityModel bridge loaded skin=" + VariantKey +
                " file=" + file + " verts=" + _model.vertexCount + " tris=" + _model.triangleCount +
                " bones=" + _model.boneCount + " animations=" + _model.animationCount +
                " preview=" + _lobbyPreview + " scale=" + _model.root.transform.localScale.x.ToString("0.#####") +
                " localPos=" + DariusLog.Vec(_model.root.transform.localPosition));
        }
        catch (Exception e)
        {
            DariusLog.Exception("TRAVELER-MODEL", e, "Failed to load Darius GLB skin model");
        }
    }
}