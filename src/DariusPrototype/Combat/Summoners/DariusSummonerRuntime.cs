using System;
using System.Reflection;
using UnityEngine;

public static class DariusSummonerRuntime
{
    public static Vector3 ResolveDirection(Hero owner, CastInfo info)
    {
        if (owner == null) return Vector3.forward;

        Vector3 dir = Vector3.zero;
        string source = "none";
        Vector3 castPoint = owner.transform.position;
        try { castPoint = info.point; } catch { }

        // Flash is configured as a move-direction skill. v0.18.4 accidentally gave castPoint
        // priority, so mouse/cursor targeting could pull Flash away from the direction the character
        // was actually moving. First use the Darius model bridge's measured world movement; this is
        // independent of EntityControl member names and directly represents the direction seen in game.
        try
        {
            DariusTravelerModelInstance traveler = owner.GetComponentInChildren<DariusTravelerModelInstance>(true);
            Vector3 measured;
            if (traveler != null && traveler.TryGetRecentMovementDirection(out measured))
            {
                dir = measured;
                source = "movement:measured-displacement";
            }
        }
        catch (Exception e)
        {
            DariusLog.DebugInfo("FLASH-DIR", "Measured movement lookup failed: " + e.GetType().Name + ": " + e.Message);
        }

        // Then try live/last EntityControl movement fields for the frame where input direction has
        // just changed but displacement has not yet been sampled.
        try
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            object control = null;
            Type heroType = owner.GetType();
            PropertyInfo controlProperty = heroType.GetProperty("Control", flags) ?? heroType.GetProperty("control", flags);
            if (controlProperty != null && controlProperty.GetIndexParameters().Length == 0)
                control = controlProperty.GetValue(owner, null);
            if (control == null)
            {
                FieldInfo controlField = heroType.GetField("Control", flags) ?? heroType.GetField("control", flags) ??
                                         heroType.GetField("<Control>k__BackingField", flags);
                if (controlField != null) control = controlField.GetValue(owner);
            }
            if (control == null)
            {
                Component[] components = owner.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    Component c = components[i];
                    if (c != null && c.GetType().Name == "EntityControl") { control = c; break; }
                }
            }

            if (control != null && dir.sqrMagnitude <= 0.04f)
            {
                Type t = control.GetType();
                string[] candidates = { "movementDirection", "moveDirection", "desiredMovementDirection", "lastMovementDirection" };
                for (int i = 0; i < candidates.Length && dir.sqrMagnitude <= 0.04f; i++)
                {
                    string name = candidates[i];
                    PropertyInfo prop = t.GetProperty(name, flags);
                    if (prop != null && prop.GetIndexParameters().Length == 0 && prop.PropertyType == typeof(Vector3))
                    {
                        try { dir = (Vector3)prop.GetValue(control, null); } catch { dir = Vector3.zero; }
                    }
                    if (dir.sqrMagnitude <= 0.04f)
                    {
                        FieldInfo field = t.GetField(name, flags) ?? t.GetField("<" + name + ">k__BackingField", flags);
                        if (field != null && field.FieldType == typeof(Vector3))
                        {
                            try { dir = (Vector3)field.GetValue(control); } catch { dir = Vector3.zero; }
                        }
                    }
                    if (dir.sqrMagnitude > 0.04f) source = "movement:" + name;
                }
                dir.y = 0f;
            }
        }
        catch (Exception e)
        {
            DariusLog.DebugInfo("FLASH-DIR", "Movement-direction reflection failed: " + e.GetType().Name + ": " + e.Message);
        }

        if (dir.sqrMagnitude <= 0.04f)
        {
            try
            {
                dir = castPoint - owner.transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.04f) source = "castPoint-fallback";
            }
            catch { dir = Vector3.zero; }
        }

        if (dir.sqrMagnitude <= 0.04f)
        {
            dir = owner.transform.forward;
            dir.y = 0f;
            source = "facing-fallback";
        }

        if (dir.sqrMagnitude <= 0.001f)
        {
            dir = Vector3.forward;
            source = "world-forward-fallback";
        }

        dir.Normalize();
        DariusLog.Info("FLASH-DIR", "source=" + source + " dir=" + DariusLog.Vec(dir) +
            " castPoint=" + DariusLog.Vec(castPoint) + " ownerPos=" + DariusLog.Vec(owner.transform.position));
        return dir;
    }
}
