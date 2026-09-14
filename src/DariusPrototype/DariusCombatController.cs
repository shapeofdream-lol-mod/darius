public sealed partial class DariusCombatController : MonoBehaviour
{
    // ===== Prototype balance =====
    public const float Q_COOLDOWN = 6.0f;

    public const float Q_WINDUP = 0.75f;

    public const float Q_INNER_RADIUS = 2.40f;

    public const float Q_OUTER_RADIUS = 4.25f;

    public const float Q_BASE_AD_RATIO = 1.20f;

    public const float Q_NO_BLEED_OUTER_AD_RATIO = 1.80f;

    public const float Q_HEAL_MAX_HP_PER_TARGET = 0.10f;

    public const float Q_HEAL_CAP_MAX_HP = 0.50f;

    public const float W_COOLDOWN = 5.0f;

    public const float W_ARM_TIME = 4.0f;

    public const float W_BONUS_AD_RATIO = 0.75f; // 175% total attack equivalent.
    public const float W_SLOW_STRENGTH = 0.90f;

    public const float W_SLOW_DURATION = 1.25f;

    public const float E_COOLDOWN = 10.0f;

    public const float E_RANGE = 5.35f;

    public const float E_HALF_ANGLE = 30.0f;

    public const float E_PULL_FRONT_DISTANCE = 1.35f;

    public const float E_SLOW_STRENGTH = 0.40f;

    public const float E_SLOW_DURATION = 1.25f;

    public const float R_COOLDOWN = 24.0f;

    public const float R_RANGE = 6.0f;

    public const float R_NO_BLEED_AD_RATIO = 1.575f; // Pass1 hotfix2: -25% legacy harness parity.
    public const float R_BLEED_BASE_AD_RATIO = 1.05f;

    public const float R_DAMAGE_PER_STACK = 0.20f; // +20% multiplicative per stack; 5 stacks = 200% of base.

    public const int BLEED_MAX_STACKS = 5;

    public const float BLEED_DURATION = 5.0f;

    public const float BLEED_TICK_INTERVAL = 1.0f;

    public const float BLEED_AD_RATIO_PER_STACK_PER_TICK = 0.05f;

    public const float NOXIAN_MIGHT_DURATION = 5.0f;

    public const float NOXIAN_MIGHT_AD_PERCENT = 50.0f;

    public Hero hero { get; private set; }

    private bool _initialized;

    private bool _hemorrhageEnabled;

    private bool _wArmed;

    private float _wArmUntil;

    private float _qReadyAt;

    private float _wReadyAt;

    private float _eReadyAt;

    private float _rReadyAt;

    private float _noxianMightUntil;

    private StatBonus _noxianMightBonus;

    private GameObject _legacyWArmVfx;

    private GameObject _legacyNoxianMightVfx;

    private readonly Dictionary<Entity, BleedState> _bleeds = new Dictionary<Entity, BleedState>();

    private readonly List<Entity> _scratchEntities = new List<Entity>(64);

    private ClientEventManager _clientEvents;

    private sealed class BleedState
    {
        public int stacks;
        public float expireAt;
        public float nextTickAt;
    }

    public void Initialize(Hero targetHero)
    {
        if (targetHero == null)
            return;

        if (_initialized && hero == targetHero)
            return;

        CleanupSubscriptions();
        hero = targetHero;
        _initialized = true;

        _clientEvents = NetworkedManagerBase<ClientEventManager>.instance;
        if (_clientEvents != null)
            _clientEvents.OnAttackHit += OnAttackHit;

        DariusLog.Info("LEGACY", "Legacy hotkey controller attached to " + DariusLog.EntityLabel(hero));
    }

    private void OnDestroy()
    {
        CleanupSubscriptions();
        ClearLegacyWArmVfx();
        RemoveNoxianMight();
    }

    private void CleanupSubscriptions()
    {
        if (_clientEvents != null)
        {
            try { _clientEvents.OnAttackHit -= OnAttackHit; } catch { }
        }
        _clientEvents = null;
    }

    private void Update()
    {
        if (!_initialized || hero == null || hero.IsNullOrInactive())
            return;

        // Legacy F5-F9 direct-cast controls are intentionally disabled in v0.13.
        // Normal gameplay uses independently equipable Memories and the Identity slot.

        if (_wArmed && Time.time > _wArmUntil)
        {
            _wArmed = false;
            ClearLegacyWArmVfx();
        }

        UpdateBleeds();
        UpdateNoxianMight();
    }
}