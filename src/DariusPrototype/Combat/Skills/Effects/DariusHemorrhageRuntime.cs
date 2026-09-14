public sealed partial class DariusHemorrhageRuntime : MonoBehaviour
{
    public const int MaxStacks = 5; // Base cap; runtime cap grows with the Identity Memory.
    public const int StackCapMilestoneEveryLevels = 5;

    public const int StackCapPerMilestone = 1;

    public const float NoxianMightAttackDamagePerExtraMaxStack = 20f;

    public const float Duration = 5f;

    public const float TickInterval = 1f;

    public const float AdRatioPerStackPerTick = 0.07f;

    public const float DamageGrowthPerAdditionalLevel = 0.20f;

    public static float AdRatioPerStackAtLevel(int level)
    {
        return DariusMemoryScaling.Linear(AdRatioPerStackPerTick, level, DamageGrowthPerAdditionalLevel);
    }

    public static int MaxStacksAtLevel(int level)
    {
        return MaxStacks + DariusMemoryScaling.MilestoneCount(level, StackCapMilestoneEveryLevels) * StackCapPerMilestone;
    }

    public static float NoxianMightAttackDamagePercentAtLevel(int level, bool warFervor)
    {
        float baseline = warFervor ? WarFervorAttackDamagePercent : NoxianMightAttackDamagePercent;
        int extraMaxStacks = Mathf.Max(0, MaxStacksAtLevel(level) - MaxStacks);
        return baseline + extraMaxStacks * NoxianMightAttackDamagePerExtraMaxStack;
    }

    public const float NoxianMightDuration = 5f;

    public const float NoxianMightAttackDamagePercent = 50f;

    public const float WarFervorAttackDamagePercent = 35f;

    public const int WarFervorRequiredApplications = 5;

    public bool isEnabled => _identityEquipped || _legacyEssenceSources > 0;

    public bool hasNoxianMight => isEnabled && Time.time < _noxianMightUntil;

    public int sourceCount => (_identityEquipped ? 1 : 0) + _legacyEssenceSources;

    public int essenceSourceCount => sourceCount; // compatibility for older callers/logs
    public int identityMemoryLevel => _identityEquipped ? _identityMemoryLevel : 1;

    public int currentMaxStacks => MaxStacksAtLevel(identityMemoryLevel);

    public float currentNoxianMightAttackDamagePercent => NoxianMightAttackDamagePercentAtLevel(identityMemoryLevel, DariusConstellationRuntime.GetWarFervorLevel(_owner) > 0);

    private Hero _owner;

    private bool _attackSubscribed;

    private bool _identityEquipped;

    private int _identityMemoryLevel = 1;

    private AbilityTrigger _identitySourceTrigger;

    private int _legacyEssenceSources;

    private float _noxianMightUntil;

    private StatBonus _noxianMightBonus;

    private GameObject _noxianMightVfx;

    private int _lastHudStackCount = -1;

    private int _lastHudDurationStep = -1;

    private readonly Dictionary<Entity, BleedState> _states = new Dictionary<Entity, BleedState>();

    private readonly List<Entity> _remove = new List<Entity>();

    private readonly Queue<float> _warFervorApplications = new Queue<float>();

    private sealed class BleedState
    {
        public int stacks;
        public float expiresAt;
        public float nextTickAt;
        public GameObject stackVfx;
        public GameObject fiveStackVfx;
    }

    public void SetIdentityEquipped(Hero owner, bool equipped, int memoryLevel = 1, AbilityTrigger sourceTrigger = null)
    {
        if (equipped && _owner != owner)
        {
            DariusLog.Info("HEM", "Owner changed from " + (_owner != null ? _owner.name : "<null>") + " to " + (owner != null ? owner.name : "<null>"));
            UnsubscribeAttack();
            _owner = owner;
            EnsureHud();
        }

        bool before = _identityEquipped;
        int beforeLevel = _identityMemoryLevel;
        _identityEquipped = equipped;
        _identityMemoryLevel = equipped ? DariusMemoryScaling.NormalizeLevel(memoryLevel) : 1;
        _identitySourceTrigger = equipped ? sourceTrigger : null;
        if (before != _identityEquipped || beforeLevel != _identityMemoryLevel)
        {
            DariusLog.Info("HEM", "Identity source " + before + " -> " + _identityEquipped +
                " memoryLevel=" + beforeLevel + " -> " + _identityMemoryLevel +
                " bleedRatioPerStack=" + AdRatioPerStackAtLevel(_identityMemoryLevel).ToString("0.###") +
                " maxStacks=" + MaxStacksAtLevel(_identityMemoryLevel) +
                " noxianAD=" + NoxianMightAttackDamagePercentAtLevel(_identityMemoryLevel, DariusConstellationRuntime.GetWarFervorLevel(_owner) > 0).ToString("0.#") + "%" +
                " owner=" + DariusLog.EntityLabel(_owner));
            if (_identityEquipped && beforeLevel != _identityMemoryLevel)
            {
                if (_noxianMightBonus != null)
                    RefreshNoxianMightStatBonus("Identity Memory level changed");
                RefreshBleedMarkersForCurrentCap();
            }
        }
        RefreshSourceState();
    }

    public void AddLegacyEssenceSource(Hero owner)
    {
        if (_owner != owner)
        {
            DariusLog.Info("HEM", "Legacy owner changed from " + (_owner != null ? _owner.name : "<null>") + " to " + (owner != null ? owner.name : "<null>"));
            UnsubscribeAttack();
            _owner = owner;
            EnsureHud();
        }
        _legacyEssenceSources++;
        DariusLog.Info("HEM", "Legacy Essence source added. count=" + _legacyEssenceSources + " owner=" + DariusLog.EntityLabel(_owner));
        RefreshSourceState();
    }

    public void RemoveLegacyEssenceSource()
    {
        int before = _legacyEssenceSources;
        _legacyEssenceSources = Mathf.Max(0, _legacyEssenceSources - 1);
        DariusLog.Info("HEM", "Legacy Essence source removed. count " + before + " -> " + _legacyEssenceSources);
        RefreshSourceState();
    }

    // Backward-compatible aliases for v0.11 callers/hot reloads.
    public void AddEssenceSource(Hero owner) { AddLegacyEssenceSource(owner); }

    public void RemoveEssenceSource() { RemoveLegacyEssenceSource(); }
}