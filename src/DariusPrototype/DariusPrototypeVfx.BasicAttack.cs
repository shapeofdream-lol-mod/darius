public static partial class DariusPrototypeVfx
{
    public static void CreateBasicAttackRangeArc(Hero owner, Vector3 forward, float range)
    {
        if (owner == null || range <= 0.05f) return;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f) forward = owner.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        // Shape of Dreams' melee readability uses a curved ground sector. The sector angle is purely
        // presentation; every outer point is exactly `range` metres from the Hero, so its radial edge
        // can never claim more or less reach than At_DariusAxe's live trigger configuration.
        const float halfAngle = 55f;
        const int arcSegments = 18;
        GameObject go = new GameObject(SkinFxName(owner, "BasicAttack_RangeArc"));
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = false;
        line.positionCount = arcSegments + 3;
        line.startWidth = 0.055f;
        line.endWidth = 0.055f;
        line.numCornerVertices = 3;
        line.numCapVertices = 2;
        Color c = SkinColor(owner, new Color(0.94f, 0.42f, 0.34f, 0.72f), new Color(1.00f, 0.63f, 0.16f, 0.82f));
        line.startColor = c;
        line.endColor = new Color(c.r, c.g, c.b, 0.42f);
        line.material = NewMaterial(c);

        Vector3 center = owner.transform.position + Vector3.up * 0.07f;
        line.SetPosition(0, center);
        for (int i = 0; i <= arcSegments; i++)
        {
            float t = i / (float)arcSegments;
            float angle = Mathf.Lerp(-halfAngle, halfAngle, t);
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * forward;
            line.SetPosition(i + 1, center + dir * range);
        }
        line.SetPosition(arcSegments + 2, center);
        UnityEngine.Object.Destroy(go, 0.30f);
        DariusLog.DebugInfoThrottled("ATK-RANGE-VFX", "arc",
            "Basic attack ground sector rendered with exact radial range=" + range.ToString("0.###") +
            "m halfAngle=" + halfAngle.ToString("0.#"), 1.0);
    }

    public static void CreateBasicAttackSwing(Hero owner, bool alternate)
    {
        if (owner == null) return;
        // Pass 5 removes the old hand-authored axe trail when an exact Riot skin system exists.
        // Classic/God-King have no separate champion BA system in the supplied current source, so
        // their model animation + exact Wwise attack SFX are left unmodified rather than fabricated.
        if (Dunkmaster(owner))
            DariusLolVfxRuntime.PlayAttached(owner, "Darius_Skin04_BA", owner.transform);
        else if (Mecha(owner))
            DariusLolVfxRuntime.PlayAttached(owner, alternate ? "Darius_Skin67_BA02" : "Darius_Skin67_BA01", owner.transform);
        DariusMedia.PlayForSkin("basic_attack", owner, DariusAudioChannel.BasicAttack, alternate ? 0.42f : 0.36f);
    }

    public static void CreateBasicAttackImpact(Hero owner, Vector3 p)
    {
        // No generic hand-made impact flash. The original LoL champion banks used by this package
        // do not expose a universal Darius basic-hit target system, so absence is preferred to a
        // fabricated substitute. Hit SFX still routes through the skin-specific audio bank.
    }

    public static void CreateBasicAttackImpact(Vector3 p) { CreateBasicAttackImpact(null, p); }

    public static void CreateQWindupTrail(Hero owner)
    {
        // Weapon-attached motion is authored inside the converted Riot system; do not layer a hand-made trail.
        if (owner == null) return;
        DariusLog.DebugInfo("LOL-VFX-BRIDGE", "Q windup uses converted Riot particle system; manual weapon trail suppressed.");
    }

    public static void CreateWAttackSwing(Hero owner)
    {
        if (owner == null) return;
        string system = LolSystem(owner, "darius_Base_W_weapon_02", "Darius_Skin15_W_Cast", "Darius_Skin04_W_Weapon_Child", "Darius_Skin67_W_Cast");
        Transform anchor = DariusSkinAnimationHooks.GetWeaponAnchor(owner);
        if (anchor == null) anchor = owner.transform;
        DariusLolVfxRuntime.PlayAttached(owner, system, anchor);
    }

    public static void CreateEWindup(Hero owner)
    {
        if (owner == null) return;
        Transform anchor = DariusSkinAnimationHooks.GetWeaponAnchor(owner);
        if (anchor == null) anchor = owner.transform;
        DariusLolVfxRuntime.PlayAttached(owner, LolSystem(owner, "darius_Base_E_weapon_trigger", "Darius_Skin15_E_Cast", null, "Darius_Skin67_E_Cast"), anchor);
        DariusMedia.PlayForSkin("e_cast", owner, 0.82f);
        DariusMedia.PlaySkillVoice(owner, "e", DariusAudioChannel.E, 0.78f);
    }

    public static void CreateRWindup(Hero owner)
    {
        if (owner == null) return;
        Transform weapon = DariusSkinAnimationHooks.GetWeaponAnchor(owner);
        if (weapon == null) weapon = owner.transform;
        DariusLolVfxRuntime.PlayAttached(owner, LolSystem(owner, "darius_Base_R_cast_axe", "Darius_Skin15_R_cast_axe", null, "Darius_Skin67_R_CastAxe"), weapon);
        if (GodKing(owner))
        {
            // Both are direct conversions from Skin15. The visible lunging beast itself is the restored
            // Wolf_Mat SKN submesh animated by the original Spell4 lion-bone animation.
            DariusLolVfxRuntime.PlayAttached(owner, "Darius_Skin15_R_Cast_Wolf", owner.transform);
            DariusLolVfxRuntime.PlayAttached(owner, "Darius_Skin15_R_Trail", weapon);
        }
        else if (Mecha(owner))
        {
            DariusLolVfxRuntime.PlayAttached(owner, "Darius_Skin67_R_CastWolf", owner.transform);
            DariusLolVfxRuntime.PlayAttached(owner, "Darius_Skin67_R_Trail", weapon);
        }
        DariusMedia.PlayForSkin("r_cast", owner, 0.96f);
        DariusMedia.PlaySkillVoice(owner, "r", DariusAudioChannel.R, 0.82f);
    }

    private static LineRenderer FallbackRing(string name, Vector3 center, float radius, Color color, float width, float lifetime)
    {
        GameObject go = new GameObject(name);
        go.transform.position = center + Vector3.up * 0.08f;
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = 64;
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = color;
        line.endColor = color;
        line.material = NewMaterial(color);
        for (int i = 0; i < 64; i++)
        {
            float a = i * Mathf.PI * 2f / 64f;
            line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
        }
        UnityEngine.Object.Destroy(go, lifetime);
        return line;
    }

    // Q needs a readable pre-impact range telegraph. The previous 0.08-0.13 alpha texture was
    // effectively invisible in Shape of Dreams' blue combat rooms. Keep the actual impact sweep
    // separate, but make both the outer blade edge and inner dead-zone boundary unambiguous.
    public static void CreateQTelegraph(Transform owner, float inner, float outer, float duration)
    {
        if (owner == null) return;
        Hero heroOwner = owner.GetComponentInParent<Hero>();
        if (heroOwner == null) return;
        // The LoL system is authored in champion-space units (Q outer radius ~425), and the runtime
        // converter applies the same 0.00921 LoL->Shape-of-Dreams scale as the champion model.
        DariusLolVfxRuntime.PlayAttached(heroOwner, LolSystem(heroOwner, "Darius_Base_Q_Ring_Windup", "Darius_Skin15_Q_Ring_Windup", "Darius_Skin04_Q_Ring_Windup", "Darius_Skin67_Q_RingWindup"), owner);
        // Riot's actual Q_Ring emitters already contain ~0.75 s authored delays. Starting this
        // system only at the gameplay impact added the same delay a second time, which is why the
        // God-King spinning flash appeared roughly one second after Darius had finished Q. Start
        // the delayed ring now, alongside the windup, so its authored delay lands on the spin.
        DariusLolVfxRuntime.PlayAttached(heroOwner, LolSystem(heroOwner, "Darius_Base_Q_Ring", "Darius_Skin15_Q_Ring", "Darius_Skin04_Q_Ring", "Darius_Skin67_Q_Ring"), owner);
        DariusLog.DebugInfo("Q-TELEGRAPH", "Converted Riot Q windup + authored-delayed ring started together inner=" + inner.ToString("0.###") +
            " outer=" + outer.ToString("0.###") + " duration=" + duration.ToString("0.###"));
        DariusMedia.PlayForSkin("q_windup", heroOwner, owner.position, 0.84f);
        DariusMedia.PlaySkillVoice(heroOwner, "q", DariusAudioChannel.Q, 0.78f);
    }

    // One sweep only. Layer a pale steel axe arc over a short blood-red shock ring.
    public static void CreateQSwing(Hero owner, Vector3 center, float inner, float outer)
    {
        if (owner == null) return;
        // Q_Ring was started at cast-start because its Riot emitters contain the windup delay.
        // Do not spawn it again here. Skin15's zero-delay activation flash belongs exactly here.
        if (GodKing(owner)) DariusLolVfxRuntime.PlayAttached(owner, "Darius_Skin15_Q_Activate", owner.transform);
        else if (Mecha(owner)) DariusLolVfxRuntime.PlayAttached(owner, "Darius_Skin67_Q_Activate", owner.transform);
    }
}