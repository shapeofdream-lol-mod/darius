// One per server-side Hero_Darius. It exists only as gameplay state; it does not create a second
// constellation UI or bypass StarEffect/Profile. DariusStarEffect remains the authority that calls
// SetStar through DariusConstellationRuntime.
public sealed partial class DariusEquipmentRuntime : MonoBehaviour
{
    private Hero _hero;

    private readonly Dictionary<string, int> _levels = new Dictionary<string, int>(StringComparer.Ordinal);

    private bool _attackSubscribed;

    private float _lastHealth = -1f;

    private float _lastCombatAt = -999f;

    private Vector3 _lastPosition;

    private bool _hasLastPosition;

    private float _trinityArmedUntil;

    private float _trinityReadyAt;

    private int _shojinStacks;

    private float _shojinExpiresAt;

    private string _lastShojinSource;

    private float _lastShojinSourceAt;

    private StatBonus _sterakBarrierBonus;

    private Coroutine _sterakBarrierRoutine;

    private float _sterakReadyAt;

    private sealed class DeferredDamage
    {
        public float perTick;
        public int ticksRemaining;
        public float nextTick;
    }

    private readonly List<DeferredDamage> _deathDance = new List<DeferredDamage>();

    private sealed class ArmorShred
    {
        public Entity target;
        public int stacks;
        public float expiresAt;
        public StatBonus bonus;
    }

    private readonly Dictionary<int, ArmorShred> _blackCleaver = new Dictionary<int, ArmorShred>();

    private readonly Dictionary<int, float> _sunderedReady = new Dictionary<int, float>();

    private readonly Dictionary<int, float> _recentKills = new Dictionary<int, float>();

    private float _deadMansMomentum;

    private StatBonus _bloodmailBonus;

    private float _nextBloodmailRefresh;

    private StatBonus _youmuuBonus;

    private bool _youmuuActive;

    private string _awooRoomToken;

    private float _awooRAdRatio = 0.25f;

    private float _awooPitch = 1f;

    private void Awake()
    {
        _hero = GetComponent<Hero>();
        if (_hero != null)
        {
            _lastHealth = _hero.currentHealth;
            _lastPosition = _hero.transform.position;
            _hasLastPosition = true;
        }
    }

    private void Start()
    {
        SubscribeAttack();
    }

    private void SubscribeAttack()
    {
        if (_attackSubscribed || !NetworkServer.active || _hero == null) return;
        try
        {
            _hero.ActorEvent_OnAttackHit += OnAttackHit;
            _attackSubscribed = true;
            DariusLog.DebugInfo("ITEM-STAR", "Subscribed equipment basic-attack listener owner=" + DariusLog.EntityLabel(_hero));
        }
        catch (Exception e) { DariusLog.Exception("ITEM-STAR", e, "Could not subscribe equipment basic-attack listener"); }
    }

    public void SetStar(string key, int level, bool active)
    {
        if (string.IsNullOrEmpty(key) || !DariusEquipmentConstellationLocalization.IsEquipmentKey(key)) return;
        if (_hero == null) _hero = GetComponent<Hero>();
        if (active) _levels[key] = Mathf.Max(1, level); else _levels.Remove(key);
        SubscribeAttack();

        if (key == DariusEquipmentStarIds.Awoo)
        {
            ResetAwooForCurrentRoom("star-state-change");
            St_Darius_NoxianGuillotine.ApplyAwooCooldownOverrideToHero(_hero, active);
            if (!active)
            {
                DariusAwooAudioRuntime audio = _hero != null ? _hero.GetComponent<DariusAwooAudioRuntime>() : null;
                if (audio != null) audio.StopPlayback();
            }
        }
        if (key == DariusEquipmentStarIds.OverlordsBloodmail) RefreshBloodmail(true);
        if (key == DariusEquipmentStarIds.YoumuusGhostblade) RefreshYoumuu(true);
        if (!active && key == DariusEquipmentStarIds.SteraksGage) RemoveSterakBarrier();
        if (!active && key == DariusEquipmentStarIds.DeathsDance) _deathDance.Clear();
        if (!active && key == DariusEquipmentStarIds.BlackCleaver) ClearBlackCleaver();
    }

    public int GetLevel(string key)
    {
        int value;
        return !string.IsNullOrEmpty(key) && _levels.TryGetValue(key, out value) ? value : 0;
    }

    public static DariusEquipmentRuntime Get(Hero hero, bool create = false)
    {
        if (hero == null) return null;
        DariusEquipmentRuntime runtime = hero.GetComponent<DariusEquipmentRuntime>();
        if (runtime == null && create) runtime = hero.gameObject.AddComponent<DariusEquipmentRuntime>();
        return runtime;
    }

    public static void SetStarForHero(Hero hero, string key, int level, bool active)
    {
        if (hero == null || !DariusEquipmentConstellationLocalization.IsEquipmentKey(key)) return;
        DariusEquipmentRuntime runtime = Get(hero, active);
        if (runtime != null) runtime.SetStar(key, level, active);
    }

    public static void NotifySkillCast(Hero hero, string source)
    {
        if (!NetworkServer.active || hero == null) return;
        DariusEquipmentRuntime runtime = Get(hero, false);
        if (runtime != null) runtime.OnSkillCast(source);
    }

    public static void NotifyPhysicalSkillHit(Hero hero, Entity target, string source, bool countsForShojin)
    {
        if (!NetworkServer.active || hero == null || target == null) return;
        DariusEquipmentRuntime runtime = Get(hero, false);
        if (runtime != null) runtime.OnPhysicalSkillHit(target, source, countsForShojin);
    }
}