public sealed class Ai_Darius_CripplingStrike : AbilityInstance
{
    protected override void OnCreate()
    {
        base.OnCreate();
        StartCoroutine(RunExecute());
    }

    private IEnumerator RunExecute()
    {
        yield return Execute(info, firstTrigger, this);
        if (NetworkServer.active) Destroy();
    }

    // The exact gameplay/VFX routine is also exposed for the SkillTrigger emergency fallback.
    // Normally this runs through a real AbilityInstance. If the runtime prefab cannot be parented
    // by the stock Actor factory, the trigger can still execute the same CastInfo after vanilla
    // cooldown/charge handling has already completed.
    public static IEnumerator Execute(CastInfo castInfo, AbilityTrigger sourceTrigger = null, Actor sourceActor = null)
    {
        int castId = DariusLog.NextId();
        Hero owner = castInfo.caster as Hero;
        if (owner == null)
        {
            DariusLog.Error("W", "cast#" + castId + " aborted: caster is not a Hero.");
            yield break;
        }

        DariusEquipmentRuntime.NotifySkillCast(owner, "W cast#" + castId);

        int memoryLevel = DariusMemoryScaling.GetLevel(sourceTrigger);
        float memoryScale = DariusMemoryScaling.Multiplier(memoryLevel);
        DariusLog.Info("W", "cast#" + castId + " cast start owner=" + DariusLog.EntityLabel(owner) +
            " memoryLevel=" + memoryLevel + " memoryScale=" + memoryScale.ToString("0.###"));
        // W is an attack modifier. Do not play the attack swing on button press;
        // At_DariusAxe.OnCastStart now owns the empowered swing timing.
        if (!NetworkServer.active)
        {
            DariusLog.DebugInfo("W", "cast#" + castId + " client visual-only branch.");
            yield break;
        }

        try
        {
            DariusCripplingStrikeRuntime runtime = owner.GetComponent<DariusCripplingStrikeRuntime>();
            if (runtime == null)
            {
                runtime = owner.gameObject.AddComponent<DariusCripplingStrikeRuntime>();
                DariusLog.DebugInfo("W", "cast#" + castId + " created DariusCripplingStrikeRuntime.");
            }
            // W only arms the next native basic attack. It deliberately does NOT reset the
            // attack timer: the current design/tooltip does not promise an auto-attack reset.
            runtime.Arm(owner, castId, memoryLevel, sourceTrigger);
            DariusLog.Info("W", "cast#" + castId + " armed next basic attack without modifying attack cooldown.");
        }
        catch (Exception e)
        {
            DariusLog.Exception("W", e, "cast#" + castId + " arm/reset failed");
        }

        yield break;
    }
}
