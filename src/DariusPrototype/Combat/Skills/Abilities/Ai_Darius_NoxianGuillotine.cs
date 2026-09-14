public sealed partial class Ai_Darius_NoxianGuillotine : AbilityInstance
{
    // Pass1 hotfix2 balance: R was substantially overtuned in Shape of Dreams. Reduce the
    // entire level-scaled baseline by 25% while preserving Hemorrhage stack multipliers and the
    // native Memory per-level growth curve. 2.60 -> 1.95, 1.80 -> 1.35.
    public const float NoEssenceAdRatio = 1.95f;

    public const float EssenceBaseAdRatio = 1.35f;

    public const float DamagePerHemorrhageStack = 0.25f;

    public const float DamageGrowthPerAdditionalLevel = 0.20f;

    // Spell4 reaches the airborne apex around 0.28 s and the axe-down impact around 0.42 s.
    public const float LeapMoveDelay = 0.28f;

    public const float ImpactDelay = 0.42f;

    private St_Darius_NoxianGuillotine _sourceDariusTrigger;

    private bool _normalTerminalReached;

    public static float DamageRatioAtLevel(bool hemorrhageActive, int stacks, int level)
    {
        float baseRatio = hemorrhageActive
            ? EssenceBaseAdRatio * (1f + DamagePerHemorrhageStack * Mathf.Max(0, stacks))
            : NoEssenceAdRatio;
        return DariusMemoryScaling.Linear(baseRatio, level, DamageGrowthPerAdditionalLevel);
    }

    protected override void OnCreate()
    {
        base.OnCreate();
        _sourceDariusTrigger = firstTrigger as St_Darius_NoxianGuillotine;
        _normalTerminalReached = false;
        StartCoroutine(RunExecute());
    }

    private IEnumerator RunExecute()
    {
        yield return Execute(info, firstTrigger, this);
        // Mark terminal before Destroy(), otherwise Unity OnDestroy would misclassify our own normal
        // cleanup as an interruption and incorrectly refund a legitimately spent non-kill R.
        _normalTerminalReached = true;
        if (_sourceDariusTrigger != null)
            _sourceDariusTrigger.NotifyNativeExecutionFinished("AbilityInstance coroutine completed");
        if (NetworkServer.active) Destroy();
    }

    private void OnDestroy()
    {
        if (!NetworkServer.active || _normalTerminalReached || _sourceDariusTrigger == null) return;
        // A native Action/attack cancellation can destroy this actor without allowing RunExecute to
        // reach the line above. Recover the whole R state instead of leaving a hidden cast lock.
        _sourceDariusTrigger.NotifyNativeExecutionInterrupted("AbilityInstance destroyed before normal terminal callback");
        _sourceDariusTrigger = null;
    }
}