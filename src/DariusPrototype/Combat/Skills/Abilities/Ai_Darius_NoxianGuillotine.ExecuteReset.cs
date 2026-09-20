using System;
using System.Collections;
using UnityEngine;
using Mirror;

public sealed partial class Ai_Darius_NoxianGuillotine : AbilityInstance
{
    private static void ResolveExecuteReset(Hero owner, AbilityTrigger sourceTrigger, int castId)
    {
            try
            {
                DariusHemorrhageRuntime passive = owner.GetComponent<DariusHemorrhageRuntime>();
                bool granted = passive != null && passive.GrantNoxianMightFromExecute("R execute cast#" + castId);
                DariusLog.Info("R-EXECUTE-MIGHT", "cast#" + castId + " kill -> immediate Noxian Might granted=" + granted +
                    " passive=" + (passive != null));
            }
            catch (Exception e)
            {
                DariusLog.Exception("R-EXECUTE-MIGHT", e, "cast#" + castId + " execute grant failed");
            }

            if (sourceTrigger != null)
            {
                St_Darius_NoxianGuillotine dariusR = sourceTrigger as St_Darius_NoxianGuillotine;
                if (dariusR != null)
                {
                    dariusR.RestoreUltimateCharge("execute reset cast#" + castId);
                    DariusLog.Info("R-RESET", "cast#" + castId + " ultimate charge restored trigger=" + sourceTrigger.name);
                }
                else
                {
                    // Compatibility fallback for an unexpected non-Darius trigger.
                    sourceTrigger.SetChargeAll(1);
                    sourceTrigger.SetCooldownTimeAll(0f, false);
                    DariusLog.Warn("R-RESET", "cast#" + castId + " used generic charge restore because source trigger type was " + sourceTrigger.GetType().Name);
                }
            }
            else
            {
                DariusLog.Error("R-RESET", "cast#" + castId + " kill confirmed but firstTrigger is null; ultimate charge was NOT restored.");
            }
            try { DariusPrototypeVfx.CreateRReset(owner, owner.transform.position); }
            catch (Exception e) { DariusLog.Exception("R-VFX", e, "cast#" + castId + " reset VFX failed"); }
    }
}