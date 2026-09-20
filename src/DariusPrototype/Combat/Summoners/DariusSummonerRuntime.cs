using System;
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

        // EntityControl already exposes synchronized agent velocity everywhere; use that as the
        // single movement-direction source instead of probing private movement members.
        try
        {
            EntityControl control = owner.Control;
            if (control != null)
            {
                dir = control.agentVelocity;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.04f) source = "movement:agentVelocity";
            }
        }
        catch (Exception e)
        {
            DariusLog.DebugInfo("FLASH-DIR", "EntityControl.agentVelocity read failed: " +
                e.GetType().Name + ": " + e.Message);
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
