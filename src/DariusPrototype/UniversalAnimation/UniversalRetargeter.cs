using System;
using System.Collections.Generic;
using UnityEngine;

namespace DariusModelAnimationRuntime
{
    internal sealed class UniversalRetargeter : MonoBehaviour
    {
        private sealed class Binding
        {
            public string semantic;
            public Transform target;
            public Quaternion baseRotation;
            public Quaternion axisCorrection;
        }

        private Hero _hero;
        private readonly List<Binding> _bindings = new List<Binding>();
        private readonly Dictionary<string, Transform> _mapped = new Dictionary<string, Transform>(StringComparer.Ordinal);
        private SodAnimationClip _clip;
        private float _time;
        private float _speed = 1f;
        private bool _loop;
        private bool _active;
        private string _clipName;

        private static readonly Dictionary<string, HumanBodyBones> HumanMap = new Dictionary<string, HumanBodyBones>(StringComparer.Ordinal)
        {
            {"hips", HumanBodyBones.Hips}, {"spine", HumanBodyBones.Spine}, {"chest", HumanBodyBones.Chest}, {"head", HumanBodyBones.Head},
            {"leftShoulder", HumanBodyBones.LeftShoulder}, {"leftUpperArm", HumanBodyBones.LeftUpperArm}, {"leftLowerArm", HumanBodyBones.LeftLowerArm}, {"leftHand", HumanBodyBones.LeftHand},
            {"rightShoulder", HumanBodyBones.RightShoulder}, {"rightUpperArm", HumanBodyBones.RightUpperArm}, {"rightLowerArm", HumanBodyBones.RightLowerArm}, {"rightHand", HumanBodyBones.RightHand},
            {"leftUpperLeg", HumanBodyBones.LeftUpperLeg}, {"leftLowerLeg", HumanBodyBones.LeftLowerLeg}, {"leftFoot", HumanBodyBones.LeftFoot}, {"leftToes", HumanBodyBones.LeftToes},
            {"rightUpperLeg", HumanBodyBones.RightUpperLeg}, {"rightLowerLeg", HumanBodyBones.RightLowerLeg}, {"rightFoot", HumanBodyBones.RightFoot}, {"rightToes", HumanBodyBones.RightToes}
        };

        private static readonly Dictionary<string, string> ChildSemantic = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            {"hips","spine"}, {"spine","chest"}, {"chest","head"},
            {"leftShoulder","leftUpperArm"}, {"leftUpperArm","leftLowerArm"}, {"leftLowerArm","leftHand"},
            {"rightShoulder","rightUpperArm"}, {"rightUpperArm","rightLowerArm"}, {"rightLowerArm","rightHand"},
            {"leftUpperLeg","leftLowerLeg"}, {"leftLowerLeg","leftFoot"}, {"leftFoot","leftToes"},
            {"rightUpperLeg","rightLowerLeg"}, {"rightLowerLeg","rightFoot"}, {"rightFoot","rightToes"}
        };

        public void Initialize(Hero hero)
        {
            _hero = hero;
            RebuildBindings();
        }

        public int MappedBoneCount { get { return _bindings.Count; } }
        public string CurrentClip { get { return _clipName; } }

        public bool Play(string animationName, bool loop, float speed)
        {
            SodAnimationClip clip = AnimationLibrary.Get(animationName);
            if (clip == null) { RuntimeLog.Warn("RETARGET", "Animation not found: " + animationName); return false; }
            if (_hero == null) return false;
            if (_bindings.Count < 5) RebuildBindings();
            if (_bindings.Count < 5)
            {
                RuntimeLog.Warn("RETARGET", "Not enough humanoid bones mapped on " + _hero.GetType().Name + ": " + _bindings.Count);
                return false;
            }
            CaptureBasesAndCorrections();
            _clip = clip; _clipName = animationName; _time = 0f; _speed = Mathf.Max(0.05f, speed); _loop = loop; _active = true;
            ApplyAt(0f);
            RuntimeLog.Info("RETARGET", "Play " + animationName + " on " + _hero.GetType().Name + " mapped=" + _bindings.Count + " loop=" + loop);
            return true;
        }

        public void Stop(bool restorePose)
        {
            if (restorePose) RestoreBases();
            _active = false; _clip = null; _clipName = null; _time = 0f;
        }

        private void LateUpdate()
        {
            if (!_active || _clip == null || _hero == null) return;
            _time += Mathf.Max(0f, Time.deltaTime) * _speed;
            if (_loop && _clip.length > 0f) _time = Mathf.Repeat(_time, _clip.length);
            else if (_time >= _clip.length)
            {
                ApplyAt(_clip.length);
                Stop(true);
                return;
            }
            ApplyAt(_time);
        }

        private void ApplyAt(float time)
        {
            for (int i = 0; i < _bindings.Count; i++)
            {
                Binding b = _bindings[i];
                if (b.target == null) continue;
                Quaternion delta = _clip.Sample(b.semantic, time);
                Quaternion corrected = b.axisCorrection * delta * Quaternion.Inverse(b.axisCorrection);
                b.target.localRotation = b.baseRotation * corrected;
            }
        }

        private void RebuildBindings()
        {
            _bindings.Clear(); _mapped.Clear();
            if (_hero == null || AnimationLibrary.Profile == null) return;
            GameObject visual = ModelUtils.ResolveVisualModel(_hero);
            Transform root = visual != null ? visual.transform : _hero.transform;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            Animator animator = root.GetComponentInChildren<Animator>(true);

            foreach (var kv in AnimationLibrary.Profile.bones)
            {
                string sem = kv.Key;
                RetargetProfile.BoneDef def = kv.Value;
                Transform target = null;
                HumanBodyBones hb;
                if (animator != null && animator.isHuman && HumanMap.TryGetValue(sem, out hb))
                {
                    try { target = animator.GetBoneTransform(hb); } catch { target = null; }
                }
                if (target == null) target = ModelUtils.FindByAliases(all, def.aliases);
                if (target != null) _mapped[sem] = target;
            }

            foreach (var kv in _mapped)
                _bindings.Add(new Binding { semantic = kv.Key, target = kv.Value, baseRotation = kv.Value.localRotation, axisCorrection = Quaternion.identity });

            CaptureBasesAndCorrections();
            RuntimeLog.Info("RETARGET", "Rig map for " + _hero.GetType().Name + ": " + _bindings.Count + "/" + AnimationLibrary.Profile.bones.Count);
        }

        private void CaptureBasesAndCorrections()
        {
            for (int i = 0; i < _bindings.Count; i++)
            {
                Binding b = _bindings[i];
                if (b.target == null) continue;
                b.baseRotation = b.target.localRotation;
                RetargetProfile.BoneDef def;
                if (!AnimationLibrary.Profile.bones.TryGetValue(b.semantic, out def)) { b.axisCorrection = Quaternion.identity; continue; }
                Vector3 targetAxis = Vector3.zero;
                string childSem;
                Transform child;
                if (ChildSemantic.TryGetValue(b.semantic, out childSem) && _mapped.TryGetValue(childSem, out child) && child != null && child.parent == b.target)
                    targetAxis = child.localPosition.normalized;
                if (targetAxis.sqrMagnitude < 0.01f) b.axisCorrection = Quaternion.identity;
                else b.axisCorrection = Quaternion.FromToRotation(def.sourceAxis, targetAxis);
            }
        }

        private void RestoreBases()
        {
            for (int i = 0; i < _bindings.Count; i++) if (_bindings[i].target != null) _bindings[i].target.localRotation = _bindings[i].baseRotation;
        }

        private void OnDestroy() { Stop(true); }
    }
}
