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

        // remaining implementation unchanged
    }
}
