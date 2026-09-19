using System;
using Mirror;

// Direct-execution Darius skills do not spawn their AbilityInstance prefabs in SoD 1.3.x because
// runtime-created Actor parenting is unsafe in that path. Keep the authored direct gameplay, but
// attribute damage/heal/cast events back to the equipped SkillTrigger so ordinary Gems/Essences can
// observe the same source Actor they would receive from a native Memory.
public static class DariusMemoryEffectBridge
{
    public static void DealPhysical(Hero owner, AbilityTrigger sourceTrigger, float amount, float procCoefficient, Entity target, string label)
    {
        DealPhysical(owner, sourceTrigger as Actor, sourceTrigger, amount, procCoefficient, target, label);
    }

    public static void DealPhysical(Hero owner, Actor sourceActor, AbilityTrigger sourceTrigger, float amount, float procCoefficient, Entity target, string label)
    {
        if (!NetworkServer.active || owner == null || target == null) return;
        try
        {
            amount *= DariusConstellationRuntime.GetLegacyApprehendPhysicalMultiplier(owner, target);
            DamageData data = owner.PhysicalDamage(amount, procCoefficient);
            Actor actor = sourceActor ?? (sourceTrigger as Actor) ?? owner;
            data = data.SetActor(actor);
            data.Dispatch(target);
            LogAttribution(sourceTrigger, label, "physical", amount, procCoefficient);
        }
        catch (Exception e)
        {
            DariusLog.Exception("ESSENCE-BRIDGE", e, "Physical damage attribution failed; falling back to Hero source. label=" + (label ?? "<null>"));
            owner.DealDamage(owner.PhysicalDamage(amount, procCoefficient), target);
        }
    }

    public static void DealPure(Hero owner, AbilityTrigger sourceTrigger, float amount, float procCoefficient, Entity target, string label)
    {
        DealPure(owner, sourceTrigger as Actor, sourceTrigger, amount, procCoefficient, target, label);
    }

    public static void DealPure(Hero owner, Actor sourceActor, AbilityTrigger sourceTrigger, float amount, float procCoefficient, Entity target, string label)
    {
        if (!NetworkServer.active || owner == null || target == null) return;
        try
        {
            DamageData data = owner.PureDamage(amount, procCoefficient);
            Actor actor = sourceActor ?? (sourceTrigger as Actor) ?? owner;
            data = data.SetActor(actor);
            data.Dispatch(target);
            LogAttribution(sourceTrigger, label, "pure", amount, procCoefficient);
        }
        catch (Exception e)
        {
            DariusLog.Exception("ESSENCE-BRIDGE", e, "Pure damage attribution failed; falling back to Hero source. label=" + (label ?? "<null>"));
            owner.DealDamage(owner.PureDamage(amount, procCoefficient), target);
        }
    }

    public static void HealSelf(Hero owner, AbilityTrigger sourceTrigger, float amount, string label)
    {
        HealSelf(owner, sourceTrigger as Actor, sourceTrigger, amount, label);
    }

    public static void HealSelf(Hero owner, Actor sourceActor, AbilityTrigger sourceTrigger, float amount, string label)
    {
        if (!NetworkServer.active || owner == null || amount <= 0f) return;
        try
        {
            HealData heal = new HealData(amount);
            Actor actor = sourceActor ?? (sourceTrigger as Actor) ?? owner;
            heal = heal.SetActor(actor);
            heal.Dispatch(owner);
            DariusLog.DebugInfoThrottled("ESSENCE-BRIDGE", "heal:" + (sourceTrigger != null ? sourceTrigger.name : "hero"),
                "Attributed Darius heal to " + (sourceTrigger != null ? sourceTrigger.name : owner.name) +
                " label=" + (label ?? "<null>") + " amount=" + amount.ToString("0.##"), 4.0);
        }
        catch (Exception e)
        {
            DariusLog.Exception("ESSENCE-BRIDGE", e, "Heal attribution failed; falling back to Hero source. label=" + (label ?? "<null>"));
            HealData heal = new HealData(amount);
            heal.SetActor(owner);
            owner.DoHeal(heal, owner);
        }
    }

    // R deliberately avoids AbilityTrigger.OnCastComplete because that path leaks inherited stock
    // presentation and used to destabilize the runtime-created resource pipeline. Re-emit only the
    // public cast-completion events that Gems subscribe to, with no spawned AbilityInstance.
    public static void NotifyCastComplete(AbilityTrigger sourceTrigger, int configIndex, CastInfo castInfo, string label)
    {
        if (sourceTrigger == null || !NetworkServer.active) return;
        try
        {
            EventInfoCast evt = new EventInfoCast
            {
                trigger = sourceTrigger,
                configIndex = configIndex,
                info = castInfo,
                instance = null
            };
            // There is intentionally no AbilityInstance. Do not fire the BeforePrepare event with
            // a null instance: Gems that mutate the spawned instance legitimately require one.
            // TriggerEvent_OnCastComplete is a trigger-event object in the current game build, not a
            // C# delegate. Invoke it through its runtime contract so this bridge does not hard-code
            // one specific Dew event implementation.
            bool emitted = TryInvokeTriggerEvent(sourceTrigger, "TriggerEvent_OnCastComplete", evt);
            if (emitted)
            {
                DariusLog.DebugInfoThrottled("ESSENCE-BRIDGE", "cast:" + sourceTrigger.name,
                    "Re-emitted native Memory cast-complete event for " + sourceTrigger.name + " label=" + (label ?? "<null>"), 2.0);
            }
            else
            {
                DariusLog.Warn("ESSENCE-BRIDGE", "Could not locate a callable TriggerEvent_OnCastComplete runtime contract for " +
                    sourceTrigger.name + "; gameplay cast remains valid, but cast-complete-only Essences may not react.");
            }
        }
        catch (Exception e)
        {
            DariusLog.Exception("ESSENCE-BRIDGE", e, "Cast-complete event bridge failed trigger=" + sourceTrigger.name + " label=" + (label ?? "<null>"));
        }
    }


    private static bool TryInvokeTriggerEvent(object owner, string memberName, object eventInfo)
    {
        if (owner == null || string.IsNullOrEmpty(memberName) || eventInfo == null) return false;
        try
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            object eventObject = null;
            Type ownerType = owner.GetType();

            for (Type t = ownerType; t != null && eventObject == null; t = t.BaseType)
            {
                System.Reflection.FieldInfo field = t.GetField(memberName, flags | System.Reflection.BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    try { eventObject = field.GetValue(owner); } catch { }
                }
            }
            if (eventObject == null)
            {
                System.Reflection.PropertyInfo property = ownerType.GetProperty(memberName, flags);
                if (property != null && property.CanRead)
                {
                    try { eventObject = property.GetValue(owner, null); } catch { }
                }
            }
            if (eventObject == null) return false;

            Delegate callback = eventObject as Delegate;
            if (callback != null)
            {
                callback.DynamicInvoke(eventInfo);
                return true;
            }

            string[] methodNames = { "Invoke", "Dispatch", "Trigger", "Raise", "Fire", "Call" };
            Type eventType = eventObject.GetType();
            for (int ni = 0; ni < methodNames.Length; ni++)
            {
                foreach (System.Reflection.MethodInfo method in eventType.GetMethods(flags))
                {
                    if (!string.Equals(method.Name, methodNames[ni], StringComparison.Ordinal)) continue;
                    System.Reflection.ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length != 1) continue;
                    Type parameterType = parameters[0].ParameterType;
                    if (!parameterType.IsInstanceOfType(eventInfo) && !parameterType.IsAssignableFrom(eventInfo.GetType())) continue;
                    method.Invoke(eventObject, new[] { eventInfo });
                    return true;
                }
            }

            DariusLog.DebugInfoThrottled("ESSENCE-BRIDGE", "event-contract:" + eventType.FullName,
                "Trigger event object had no supported one-argument invocation method. member=" + memberName +
                " type=" + eventType.FullName, 10.0);
        }
        catch (Exception e)
        {
            DariusLog.Exception("ESSENCE-BRIDGE", e, "Reflection trigger-event invocation failed member=" + memberName);
        }
        return false;
    }

    private static void LogAttribution(AbilityTrigger sourceTrigger, string label, string damageType, float amount, float procCoefficient)
    {
        if (sourceTrigger == null) return;
        DariusLog.DebugInfoThrottled("ESSENCE-BRIDGE", "damage:" + sourceTrigger.name + ":" + damageType,
            "Attributed Darius " + damageType + " damage to Memory=" + sourceTrigger.name +
            " label=" + (label ?? "<null>") + " amount=" + amount.ToString("0.##") +
            " proc=" + procCoefficient.ToString("0.##"), 3.0);
    }
}
