using UnityEngine;

// Serialized skin-specific native presentation metadata.
// This is data only: model lifecycle belongs to SoD EntityModel/EntityAnimation.
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
