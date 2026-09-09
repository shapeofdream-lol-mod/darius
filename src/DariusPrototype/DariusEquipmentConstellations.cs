using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Mirror;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

// Pass 2 equipment constellation layer.
// Equipment stars import only the recognizable item mechanism; they never grant the item's base
// stat line. All item artwork is Riot's original Data Dragon icon. Flexible is reserved for stars
// that actually rewrite a Darius skill mechanic (Stridebreaker -> W, Awoo -> R).
public static class DariusEquipmentStarIds
{
    public const string TrinityForce = "Se_Star_Darius_D_ItemTrinityForce";
    public const string BlackCleaver = "Se_Star_Darius_D_ItemBlackCleaver";
    public const string SpearOfShojin = "Se_Star_Darius_D_ItemSpearOfShojin";

    public const string SteraksGage = "Se_Star_Darius_L_ItemSteraksGage";
    public const string DeathsDance = "Se_Star_Darius_L_ItemDeathsDance";
    public const string OverlordsBloodmail = "Se_Star_Darius_L_ItemOverlordsBloodmail";
    public const string DeadMansPlate = "Se_Star_Darius_I_ItemDeadMansPlate";

    public const string SunderedSky = "Se_Star_Darius_I_ItemSunderedSky";
    public const string YoumuusGhostblade = "Se_Star_Darius_I_ItemYoumuusGhostblade";

    // Flexible: W becomes an AoE breaking strike instead of merely receiving a standalone proc.
    public const string Stridebreaker = "Se_Star_Darius_F_ItemStridebreaker";
    // Flexible: rewrites R's room-local damage/cooldown/reward loop.
    public const string Awoo = "Se_Star_Darius_F_Awoo";
}

public sealed class Se_Star_Darius_D_ItemTrinityForce : DariusStarEffect { protected override string DariusStarKey => DariusEquipmentStarIds.TrinityForce; }
public sealed class Se_Star_Darius_D_ItemBlackCleaver : DariusStarEffect { protected override string DariusStarKey => DariusEquipmentStarIds.BlackCleaver; }
public sealed class Se_Star_Darius_D_ItemSpearOfShojin : DariusStarEffect { protected override string DariusStarKey => DariusEquipmentStarIds.SpearOfShojin; }
public sealed class Se_Star_Darius_L_ItemSteraksGage : DariusStarEffect { protected override string DariusStarKey => DariusEquipmentStarIds.SteraksGage; }
public sealed class Se_Star_Darius_L_ItemDeathsDance : DariusStarEffect { protected override string DariusStarKey => DariusEquipmentStarIds.DeathsDance; }
public sealed class Se_Star_Darius_L_ItemOverlordsBloodmail : DariusStarEffect { protected override string DariusStarKey => DariusEquipmentStarIds.OverlordsBloodmail; }
public sealed class Se_Star_Darius_I_ItemSunderedSky : DariusStarEffect { protected override string DariusStarKey => DariusEquipmentStarIds.SunderedSky; }
public sealed class Se_Star_Darius_F_ItemStridebreaker : DariusStarEffect { protected override string DariusStarKey => DariusEquipmentStarIds.Stridebreaker; }
public sealed class Se_Star_Darius_I_ItemDeadMansPlate : DariusStarEffect { protected override string DariusStarKey => DariusEquipmentStarIds.DeadMansPlate; }
public sealed class Se_Star_Darius_I_ItemYoumuusGhostblade : DariusStarEffect { protected override string DariusStarKey => DariusEquipmentStarIds.YoumuusGhostblade; }
public sealed class Se_Star_Darius_F_Awoo : DariusStarEffect
{
    protected override string DariusStarKey => DariusEquipmentStarIds.Awoo;
    protected override int FallbackMaxLevel => 1;
}

public static class DariusEquipmentConstellationLocalization
{
    private static readonly Dictionary<string, string> Names = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { DariusEquipmentStarIds.TrinityForce, "三相之力" },
        { DariusEquipmentStarIds.BlackCleaver, "黑色切割者" },
        { DariusEquipmentStarIds.SpearOfShojin, "朔极之矛" },
        { DariusEquipmentStarIds.SteraksGage, "斯特拉克的挑战护手" },
        { DariusEquipmentStarIds.DeathsDance, "死亡之舞" },
        { DariusEquipmentStarIds.OverlordsBloodmail, "霸王血铠" },
        { DariusEquipmentStarIds.SunderedSky, "焚天" },
        { DariusEquipmentStarIds.Stridebreaker, "挺进破坏者" },
        { DariusEquipmentStarIds.DeadMansPlate, "亡者的板甲" },
        { DariusEquipmentStarIds.YoumuusGhostblade, "幽梦之灵" },
        { DariusEquipmentStarIds.Awoo, "嗷呜！！！" }
    };

    private static readonly Dictionary<string, string> Descriptions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { DariusEquipmentStarIds.TrinityForce, "施放Q/W/E/R后获得【咒刃】，4秒内下一次普攻额外造成130%/150%/170%/195%攻击力的物理伤害。咒刃有1.5秒独立冷却。" },
        { DariusEquipmentStarIds.BlackCleaver, "普攻、Q与【出血】造成物理伤害时，对该敌人叠加1层【切割】，持续6秒，最多5层；每层使其护甲降低5%/5.5%/6%/6.5%。不同敌人独立计算。" },
        { DariusEquipmentStarIds.SpearOfShojin, "Q或W造成技能伤害时获得1层【龙之力】，持续6秒，最多4层；每层使之后的Q与W伤害提高5%/6%/7%/8.5%。同一次技能命中多个敌人只叠1层。" },
        { DariusEquipmentStarIds.SteraksGage, "生命值首次降至35%以下时获得持续5秒的【救主灵刃】：以临时生命屏障承受相当于最大生命值25%/30%/35%/40%的伤害。冷却30/28/26/24秒。" },
        { DariusEquipmentStarIds.DeathsDance, "受到的5%/10%/15%/20%伤害不会立即结算，而是在3秒内以纯粹伤害均匀承受。击杀敌人会清除尚未结算延迟伤害的10%/20%/30%/40%，不额外回复生命值。" },
        { DariusEquipmentStarIds.OverlordsBloodmail, "将最大生命值转化为固定攻击力；每点最大生命按0.010/0.0125/0.015/0.018转化，来自生命值的部分最多提供100攻击力。生命值越低时额外获得至多12/15/18/22攻击力。" },
        { DariusEquipmentStarIds.SunderedSky, "每个敌人拥有独立的【焚天】充能。充能就绪时，对该敌人的下一次普攻额外造成100%/115%/130%/150%攻击力物理伤害，并回复5%/6%/7%/8%已损失生命值。" },
        { DariusEquipmentStarIds.Stridebreaker, "W【致残打击】命中时会额外释放一次【破阵冲击】：对目标周围3米所有敌人造成60%/70%/80%/95%攻击力物理伤害并减速35%/40%/45%/50%，持续1.5秒。" },
        { DariusEquipmentStarIds.DeadMansPlate, "移动会积累【气势】，最多100层。下一次普攻消耗全部气势并按层数造成至多50%/60%/70%/85%攻击力的额外物理伤害；满层攻击额外造成强减速。" },
        { DariusEquipmentStarIds.YoumuusGhostblade, "脱离战斗2秒后进入【幽魂步伐】，移动速度提高15%/18%/21%/25%；造成或受到伤害时退出，重新脱战后再次获得。" },
        { DariusEquipmentStarIds.Awoo, "每个房间开始时，R【诺克萨斯断头台】改为25%攻击力加成且冷却时间减半。每次使用R开始播放Animals；R完成击杀时本房间R攻击力加成永久+5%并将播放倍速+0.1，未完成击杀则倍速-0.1；两种结果都会从头重新播放。房间结束后全部重置。" }
    };

    public static bool IsEquipmentKey(string key)
    {
        return Normalize(key) != null;
    }

    public static bool TryName(string key, out string value)
    {
        value = null;
        string id = Normalize(key);
        return id != null && (DariusLanguage.IsEnglish
            ? DariusEnglishLocalization.TryEquipmentName(id, out value)
            : Names.TryGetValue(id, out value));
    }

    public static bool TryDescription(string key, out string value)
    {
        value = null;
        string id = Normalize(key);
        if (id == null) return false;
        if (!string.IsNullOrEmpty(key) && (key.EndsWith(".lore", StringComparison.OrdinalIgnoreCase) || key.EndsWith("_lore", StringComparison.OrdinalIgnoreCase)))
        {
            value = string.Empty;
            return true;
        }
        return DariusLanguage.IsEnglish
            ? DariusEnglishLocalization.TryEquipmentDescription(id, out value)
            : Descriptions.TryGetValue(id, out value);
    }

    private static string Normalize(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        foreach (string id in Names.Keys)
        {
            if (key.IndexOf(id, StringComparison.OrdinalIgnoreCase) >= 0 ||
                key.IndexOf(id.Replace("Se_", string.Empty), StringComparison.OrdinalIgnoreCase) >= 0)
                return id;
        }
        return null;
    }
}

// One per server-side Hero_Darius. It exists only as gameplay state; it does not create a second
// constellation UI or bypass StarEffect/Profile. DariusStarEffect remains the authority that calls
// SetStar through DariusConstellationRuntime.
public sealed class DariusEquipmentRuntime : MonoBehaviour
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

    // W is a reinforced basic attack. The native attack-hit event already contributes Black Cleaver,
    // so W uses this Shojin-only entry to avoid applying two Cleaver stacks for one impact.
    public static void NotifyShojinSkillHit(Hero hero, string source)
    {
        if (!NetworkServer.active || hero == null) return;
        DariusEquipmentRuntime runtime = Get(hero, false);
        if (runtime != null) runtime.AddShojinStack(source);
    }

    public static float ModifyBasicSkillDamage(Hero hero, float amount)
    {
        DariusEquipmentRuntime runtime = Get(hero, false);
        return runtime != null ? runtime.ApplyShojinDamage(amount) : amount;
    }

    public static void NotifyKill(Hero hero, Entity victim, string source)
    {
        if (!NetworkServer.active || hero == null || victim == null) return;
        DariusEquipmentRuntime runtime = Get(hero, false);
        if (runtime != null) runtime.OnKill(victim, source);
    }

    public static void ApplyStridebreakerW(Hero hero, Entity primaryTarget, AbilityTrigger sourceTrigger, Actor sourceActor, string source)
    {
        if (!NetworkServer.active || hero == null || primaryTarget == null) return;
        DariusEquipmentRuntime runtime = Get(hero, false);
        if (runtime != null) runtime.OnStridebreakerW(primaryTarget, sourceTrigger, sourceActor, source);
    }

    public static bool TryGetAwooRRatio(Hero hero, out float ratio)
    {
        ratio = 0f;
        DariusEquipmentRuntime runtime = Get(hero, false);
        if (runtime == null || runtime.GetLevel(DariusEquipmentStarIds.Awoo) <= 0) return false;
        runtime.CheckAwooRoom();
        ratio = runtime._awooRAdRatio;
        return true;
    }

    public static bool NotifyAwooRCast(Hero hero)
    {
        if (!NetworkServer.active || hero == null) return false;
        DariusEquipmentRuntime runtime = Get(hero, false);
        if (runtime == null || runtime.GetLevel(DariusEquipmentStarIds.Awoo) <= 0) return false;
        runtime.CheckAwooRoom();
        runtime.RestartAnimals();
        DariusLog.Info("STAR-AWOO", "R cast started room=" + runtime._awooRoomToken +
            " ratio=" + runtime._awooRAdRatio.ToString("0.00") + " pitch=" + runtime._awooPitch.ToString("0.0"));
        return true;
    }

    public static void NotifyAwooRResult(Hero hero, bool killed, string source)
    {
        if (!NetworkServer.active || hero == null) return;
        DariusEquipmentRuntime runtime = Get(hero, false);
        if (runtime == null || runtime.GetLevel(DariusEquipmentStarIds.Awoo) <= 0) return;
        runtime.CheckAwooRoom();
        if (killed)
        {
            runtime._awooRAdRatio += 0.05f;
            runtime._awooPitch = Mathf.Clamp(runtime._awooPitch + 0.1f, 0.5f, 2.0f);
        }
        else
        {
            runtime._awooPitch = Mathf.Clamp(runtime._awooPitch - 0.1f, 0.5f, 2.0f);
        }
        runtime.RestartAnimals();
        DariusLog.Info("STAR-AWOO", "R result killed=" + killed + " source=" + source +
            " roomRatio=" + runtime._awooRAdRatio.ToString("0.00") + " pitch=" + runtime._awooPitch.ToString("0.0"));
    }

    private void OnSkillCast(string source)
    {
        int trinity = GetLevel(DariusEquipmentStarIds.TrinityForce);
        if (trinity > 0 && Time.time >= _trinityReadyAt)
        {
            _trinityArmedUntil = Time.time + 4f;
            DariusLog.DebugInfo("ITEM-TRINITY", "Spellblade armed source=" + source + " until=" + _trinityArmedUntil.ToString("0.00"));
        }
    }

    private float ApplyShojinDamage(float amount)
    {
        int level = GetLevel(DariusEquipmentStarIds.SpearOfShojin);
        if (level <= 0 || _shojinStacks <= 0 || Time.time >= _shojinExpiresAt) return amount;
        float[] perStack = { 0.05f, 0.06f, 0.07f, 0.085f };
        float mult = 1f + perStack[Mathf.Clamp(level, 1, perStack.Length) - 1] * _shojinStacks;
        return amount * mult;
    }

    private void OnPhysicalSkillHit(Entity target, string source, bool countsForShojin)
    {
        if (target == null) return;
        _lastCombatAt = Time.time;
        ApplyBlackCleaver(target, source);
        if (countsForShojin) AddShojinStack(source);
    }

    private void AddShojinStack(string source)
    {
        int level = GetLevel(DariusEquipmentStarIds.SpearOfShojin);
        if (level <= 0) return;
        // Q is one cast even when it catches ten enemies. The cast serial embedded in source keeps
        // AoE from instantly jumping from zero to four stacks.
        if (!string.IsNullOrEmpty(source) && string.Equals(source, _lastShojinSource, StringComparison.Ordinal) && Time.time - _lastShojinSourceAt < 0.35f)
            return;
        _lastShojinSource = source;
        _lastShojinSourceAt = Time.time;
        _shojinStacks = Mathf.Clamp(_shojinStacks + 1, 0, 4);
        _shojinExpiresAt = Time.time + 6f;
        DariusLog.DebugInfo("ITEM-SHOJIN", "source=" + source + " stacks=" + _shojinStacks + "/4 level=" + level);
    }

    private void OnAttackHit(EventInfoAttackHit info)
    {
        if (!NetworkServer.active || _hero == null || info.attacker != _hero || info.victim == null) return;
        if (!_hero.GetRelation(info.victim).HasFlag(EntityRelation.Enemy)) return;
        _lastCombatAt = Time.time;

        TriggerTrinity(info.victim);
        TriggerSunderedSky(info.victim);
        TriggerDeadMansPlate(info.victim);
        ApplyBlackCleaver(info.victim, "basic attack");
    }

    private void TriggerTrinity(Entity target)
    {
        int level = GetLevel(DariusEquipmentStarIds.TrinityForce);
        if (level <= 0 || Time.time > _trinityArmedUntil || Time.time < _trinityReadyAt || target == null) return;
        _trinityArmedUntil = 0f;
        _trinityReadyAt = Time.time + 1.5f;
        float[] ratios = { 1.30f, 1.50f, 1.70f, 1.95f };
        float damage = _hero.Status.finalStats.attackDamage * ratios[Mathf.Clamp(level, 1, ratios.Length) - 1];
        DariusMemoryEffectBridge.DealPhysical(_hero, _hero, null, damage, 0.35f, target, "Trinity Force Spellblade");
        DariusLog.Info("ITEM-TRINITY", "Spellblade consumed target=" + DariusLog.EntityLabel(target) + " damage=" + damage.ToString("0.##"));
        NotifyEquipmentKillIfNeeded(target, "Trinity Force");
    }

    private void ApplyBlackCleaver(Entity target, string source)
    {
        int level = GetLevel(DariusEquipmentStarIds.BlackCleaver);
        if (level <= 0 || target == null || target.Status == null) return;
        int id;
        try { id = target.GetInstanceID(); } catch { return; }
        ArmorShred entry;
        if (!_blackCleaver.TryGetValue(id, out entry) || entry == null || entry.target != target)
        {
            entry = new ArmorShred { target = target, stacks = 0, expiresAt = 0f, bonus = null };
            _blackCleaver[id] = entry;
        }
        entry.stacks = Mathf.Clamp(entry.stacks + 1, 1, 5);
        entry.expiresAt = Time.time + 6f;
        if (entry.bonus != null)
        {
            try { target.Status.RemoveStatBonus(entry.bonus); } catch { }
        }
        float[] shredPerStack = { 5f, 5.5f, 6f, 6.5f };
        entry.bonus = new StatBonus();
        float amount = -shredPerStack[Mathf.Clamp(level, 1, shredPerStack.Length) - 1] * entry.stacks;
        if (!DariusConstellationReflection.SetFloat(entry.bonus, amount, "armorPercentage", "armorPercent"))
        {
            DariusLog.Warn("ITEM-CLEAVER", "Could not resolve StatBonus armor-percent field; skipping this shred refresh instead of hard-binding a private game field.");
            entry.bonus = null;
            return;
        }
        target.Status.AddStatBonus(entry.bonus);
        DariusLog.DebugInfo("ITEM-CLEAVER", "source=" + source + " target=" + DariusLog.EntityLabel(target) +
            " stacks=" + entry.stacks + "/5 armor%=" + amount.ToString("0.#"));
    }

    private void TriggerSunderedSky(Entity target)
    {
        int level = GetLevel(DariusEquipmentStarIds.SunderedSky);
        if (level <= 0 || target == null) return;
        int id;
        try { id = target.GetInstanceID(); } catch { return; }
        float ready;
        if (_sunderedReady.TryGetValue(id, out ready) && Time.time < ready) return;
        float[] cds = { 6f, 5.7f, 5.4f, 5.0f };
        _sunderedReady[id] = Time.time + cds[Mathf.Clamp(level, 1, cds.Length) - 1];

        float[] ratios = { 1.00f, 1.15f, 1.30f, 1.50f };
        float damage = _hero.Status.finalStats.attackDamage * ratios[Mathf.Clamp(level, 1, ratios.Length) - 1];
        DariusMemoryEffectBridge.DealPhysical(_hero, _hero, null, damage, 0.35f, target, "Sundered Sky");

        if (_hero.Status != null)
        {
            float missing = Mathf.Max(0f, _hero.Status.maxHealth - _hero.currentHealth);
            float[] healFractions = { 0.05f, 0.06f, 0.07f, 0.08f };
            float heal = missing * healFractions[Mathf.Clamp(level, 1, healFractions.Length) - 1];
            if (heal > 0.01f) DariusMemoryEffectBridge.HealSelf(_hero, _hero, null, heal, "Sundered Sky");
        }
        DariusLog.Info("ITEM-SUNDERED", "Primed hit target=" + DariusLog.EntityLabel(target) + " damage=" + damage.ToString("0.##"));
        NotifyEquipmentKillIfNeeded(target, "Sundered Sky");
    }

    private void TriggerDeadMansPlate(Entity target)
    {
        int level = GetLevel(DariusEquipmentStarIds.DeadMansPlate);
        if (level <= 0 || target == null || _deadMansMomentum <= 0.01f) return;
        float normalized = Mathf.Clamp01(_deadMansMomentum / 100f);
        float[] ratios = { 0.50f, 0.60f, 0.70f, 0.85f };
        float damage = _hero.Status.finalStats.attackDamage * ratios[Mathf.Clamp(level, 1, ratios.Length) - 1] * normalized;
        bool wasFull = _deadMansMomentum >= 99.5f;
        _deadMansMomentum = 0f;
        DariusMemoryEffectBridge.DealPhysical(_hero, _hero, null, damage, 0.25f, target, "Dead Man's Plate Momentum");
        if (wasFull)
        {
            float[] slows = { 0.40f, 0.45f, 0.50f, 0.55f };
            DariusSlowHelper.Apply(target, slows[Mathf.Clamp(level, 1, slows.Length) - 1], 1.25f, "Dead Man's Plate full Momentum");
        }
        DariusLog.Info("ITEM-DEADMAN", "Momentum consumed ratio=" + normalized.ToString("0.00") + " full=" + wasFull + " damage=" + damage.ToString("0.##"));
        NotifyEquipmentKillIfNeeded(target, "Dead Man's Plate");
    }

    private void OnStridebreakerW(Entity primaryTarget, AbilityTrigger sourceTrigger, Actor sourceActor, string source)
    {
        int level = GetLevel(DariusEquipmentStarIds.Stridebreaker);
        if (level <= 0 || primaryTarget == null || _hero == null) return;
        float[] ratios = { 0.60f, 0.70f, 0.80f, 0.95f };
        float[] slows = { 0.35f, 0.40f, 0.45f, 0.50f };
        float ratio = ratios[Mathf.Clamp(level, 1, ratios.Length) - 1];
        float slow = slows[Mathf.Clamp(level, 1, slows.Length) - 1];
        float damage = _hero.Status.finalStats.attackDamage * ratio;
        Vector3 center = primaryTarget.transform.position;
        Collider[] hits = Physics.OverlapSphere(center, 3.0f);
        HashSet<int> seen = new HashSet<int>();
        int hitCount = 0;
        for (int i = 0; i < hits.Length; i++)
        {
            Entity enemy = hits[i] != null ? hits[i].GetComponentInParent<Entity>() : null;
            if (enemy == null || enemy.IsNullInactiveDeadOrKnockedOut()) continue;
            int id;
            try { id = enemy.GetInstanceID(); } catch { continue; }
            if (!seen.Add(id) || !_hero.GetRelation(enemy).HasFlag(EntityRelation.Enemy)) continue;
            DariusMemoryEffectBridge.DealPhysical(_hero, sourceActor, sourceTrigger, damage, 0.25f, enemy, "Stridebreaker W pulse");
            DariusSlowHelper.Apply(enemy, slow, 1.5f, "Stridebreaker W pulse");
            hitCount++;
            NotifyEquipmentKillIfNeeded(enemy, "Stridebreaker W");
        }
        DariusLog.Info("ITEM-STRIDE", "W mechanism pulse source=" + source + " targets=" + hitCount + " radius=3 damageEach=" + damage.ToString("0.##"));
    }

    private void OnOwnerDamaged(float rawDamage)
    {
        if (rawDamage <= 0.01f || _hero == null || _hero.Status == null) return;
        _lastCombatAt = Time.time;

        int deathDance = GetLevel(DariusEquipmentStarIds.DeathsDance);
        if (deathDance > 0)
        {
            float[] fractions = { 0.05f, 0.10f, 0.15f, 0.20f };
            float deferred = rawDamage * fractions[Mathf.Clamp(deathDance, 1, fractions.Length) - 1];
            if (deferred > 0.01f)
            {
                // Damage already arrived through the native chain. Refund only the deferred slice now,
                // then feed that exact slice back through DamageData over six half-second ticks.
                DariusMemoryEffectBridge.HealSelf(_hero, _hero, null, deferred, "Death's Dance defer");
                _deathDance.Add(new DeferredDamage { perTick = deferred / 6f, ticksRemaining = 6, nextTick = Time.time + 0.5f });
                DariusLog.DebugInfo("ITEM-DEATHDANCE", "Deferred=" + deferred.ToString("0.##") + " raw=" + rawDamage.ToString("0.##") + " activeQueues=" + _deathDance.Count);
            }
        }

        TryTriggerSterak();
    }

    private void TryTriggerSterak()
    {
        int level = GetLevel(DariusEquipmentStarIds.SteraksGage);
        if (level <= 0 || _hero == null || _hero.Status == null || Time.time < _sterakReadyAt) return;
        float max = Mathf.Max(1f, _hero.Status.maxHealth);
        if (_hero.currentHealth / max > 0.35f) return;

        float[] barrier = { 0.25f, 0.30f, 0.35f, 0.40f };
        float[] cooldown = { 30f, 28f, 26f, 24f };
        float fraction = barrier[Mathf.Clamp(level, 1, barrier.Length) - 1];
        _sterakReadyAt = Time.time + cooldown[Mathf.Clamp(level, 1, cooldown.Length) - 1];
        RemoveSterakBarrier();

        _sterakBarrierBonus = new StatBonus();
        DariusConstellationReflection.SetFloat(_sterakBarrierBonus, fraction * 100f, "maxHealthPercentage", "maxHealthPercent", "healthPercentage");
        _hero.Status.AddStatBonus(_sterakBarrierBonus);
        float temporaryHealth = max * fraction;
        DariusMemoryEffectBridge.HealSelf(_hero, _hero, null, temporaryHealth, "Sterak Lifeline temporary barrier");
        _sterakBarrierRoutine = StartCoroutine(RemoveSterakAfter(5f));
        DariusLog.Info("ITEM-STERAK", "Lifeline triggered temporaryHealth=" + temporaryHealth.ToString("0.##") + " duration=5 cooldown=" + cooldown[Mathf.Clamp(level,1,cooldown.Length)-1].ToString("0.#"));
    }

    private IEnumerator RemoveSterakAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        RemoveSterakBarrier();
        _sterakBarrierRoutine = null;
    }

    private void RemoveSterakBarrier()
    {
        if (_sterakBarrierBonus != null && _hero != null && _hero.Status != null)
        {
            try { _hero.Status.RemoveStatBonus(_sterakBarrierBonus); } catch { }
        }
        _sterakBarrierBonus = null;
    }

    private void TickDeathDance()
    {
        if (_deathDance.Count == 0 || _hero == null || _hero.IsNullOrInactive()) return;
        for (int i = _deathDance.Count - 1; i >= 0; i--)
        {
            DeferredDamage d = _deathDance[i];
            if (d == null || d.ticksRemaining <= 0) { _deathDance.RemoveAt(i); continue; }
            if (Time.time < d.nextTick) continue;
            d.nextTick += 0.5f;
            d.ticksRemaining--;
            try
            {
                DamageData damage = _hero.PureDamage(d.perTick, 0f).SetActor(_hero);
                damage.Dispatch(_hero);
            }
            catch (Exception e)
            {
                DariusLog.Exception("ITEM-DEATHDANCE", e, "Deferred self-damage dispatch failed");
            }
            if (d.ticksRemaining <= 0) _deathDance.RemoveAt(i);
        }
    }

    private void OnKill(Entity victim, string source)
    {
        int victimId;
        try { victimId = victim.GetInstanceID(); } catch { return; }
        float lastResolved;
        if (_recentKills.TryGetValue(victimId, out lastResolved) && Time.time - lastResolved < 1.5f) return;
        _recentKills[victimId] = Time.time;

        _lastCombatAt = Time.time;
        int deathDance = GetLevel(DariusEquipmentStarIds.DeathsDance);
        if (deathDance > 0 && _deathDance.Count > 0)
        {
            float outstanding = 0f;
            for (int i = 0; i < _deathDance.Count; i++)
                if (_deathDance[i] != null) outstanding += _deathDance[i].perTick * _deathDance[i].ticksRemaining;
            float[] clearFractions = { 0.10f, 0.20f, 0.30f, 0.40f };
            float clearFraction = clearFractions[Mathf.Clamp(deathDance, 1, clearFractions.Length) - 1];
            for (int i = _deathDance.Count - 1; i >= 0; i--)
            {
                DeferredDamage deferred = _deathDance[i];
                if (deferred == null || deferred.ticksRemaining <= 0)
                {
                    _deathDance.RemoveAt(i);
                    continue;
                }
                deferred.perTick *= 1f - clearFraction;
            }
            float cleared = outstanding * clearFraction;
            DariusLog.Info("ITEM-DEATHDANCE", "Kill cleared=" + cleared.ToString("0.##") + " of outstanding=" + outstanding.ToString("0.##") +
                " clear%=" + (clearFraction * 100f).ToString("0.#") + " source=" + source);
        }
    }

    private void RefreshBloodmail(bool force)
    {
        int level = GetLevel(DariusEquipmentStarIds.OverlordsBloodmail);
        if (!force && Time.time < _nextBloodmailRefresh) return;
        _nextBloodmailRefresh = Time.time + 0.25f;
        if (_bloodmailBonus != null && _hero != null && _hero.Status != null)
        {
            try { _hero.Status.RemoveStatBonus(_bloodmailBonus); } catch { }
            _bloodmailBonus = null;
        }
        if (level <= 0 || _hero == null || _hero.Status == null) return;

        float maxHealth = Mathf.Max(0f, _hero.Status.maxHealth);
        float hpRatio = maxHealth > 0f ? Mathf.Clamp01(_hero.currentHealth / maxHealth) : 1f;
        float[] hpConversion = { 0.010f, 0.0125f, 0.015f, 0.018f }; // flat AD per max-HP point
        float[] lowHealthCap = { 12f, 15f, 18f, 22f };
        int idx = Mathf.Clamp(level, 1, hpConversion.Length) - 1;
        float fromHealth = Mathf.Min(100f, maxHealth * hpConversion[idx]);
        float fromMissing = (1f - hpRatio) * lowHealthCap[idx];
        _bloodmailBonus = new StatBonus();
        _bloodmailBonus.attackDamageFlat = fromHealth + fromMissing;
        _hero.Status.AddStatBonus(_bloodmailBonus);
    }

    private void RefreshYoumuu(bool force)
    {
        int level = GetLevel(DariusEquipmentStarIds.YoumuusGhostblade);
        bool should = level > 0 && Time.time - _lastCombatAt >= 2f;
        if (!force && should == _youmuuActive) return;
        if (_youmuuBonus != null && _hero != null && _hero.Status != null)
        {
            try { _hero.Status.RemoveStatBonus(_youmuuBonus); } catch { }
            _youmuuBonus = null;
        }
        _youmuuActive = should;
        if (!should || _hero == null || _hero.Status == null) return;
        float[] speed = { 15f, 18f, 21f, 25f };
        _youmuuBonus = new StatBonus();
        _youmuuBonus.movementSpeedPercentage = speed[Mathf.Clamp(level, 1, speed.Length) - 1];
        _hero.Status.AddStatBonus(_youmuuBonus);
        DariusLog.DebugInfo("ITEM-YOUMUU", "Ghostwalk active move%=" + _youmuuBonus.movementSpeedPercentage);
    }

    private void TickMovement()
    {
        if (_hero == null) return;
        Vector3 p = _hero.transform.position;
        if (_hasLastPosition)
        {
            Vector3 delta = p - _lastPosition;
            delta.y = 0f;
            float dist = delta.magnitude;
            if (dist > 0f && dist < 10f && GetLevel(DariusEquipmentStarIds.DeadMansPlate) > 0)
                _deadMansMomentum = Mathf.Clamp(_deadMansMomentum + dist * 5f, 0f, 100f);
        }
        _lastPosition = p;
        _hasLastPosition = true;
    }

    private void TickBlackCleaver()
    {
        if (_blackCleaver.Count == 0) return;
        List<int> remove = null;
        foreach (KeyValuePair<int, ArmorShred> kv in _blackCleaver)
        {
            ArmorShred entry = kv.Value;
            if (entry == null || entry.target == null || entry.target.IsNullInactiveDeadOrKnockedOut() || Time.time >= entry.expiresAt)
            {
                if (entry != null && entry.bonus != null && entry.target != null && entry.target.Status != null)
                {
                    try { entry.target.Status.RemoveStatBonus(entry.bonus); } catch { }
                }
                if (remove == null) remove = new List<int>();
                remove.Add(kv.Key);
            }
        }
        if (remove != null) for (int i = 0; i < remove.Count; i++) _blackCleaver.Remove(remove[i]);
    }

    private void ClearBlackCleaver()
    {
        foreach (ArmorShred entry in _blackCleaver.Values)
        {
            if (entry != null && entry.bonus != null && entry.target != null && entry.target.Status != null)
            {
                try { entry.target.Status.RemoveStatBonus(entry.bonus); } catch { }
            }
        }
        _blackCleaver.Clear();
    }

    private void CheckAwooRoom()
    {
        if (GetLevel(DariusEquipmentStarIds.Awoo) <= 0) return;
        string scene = string.Empty;
        try { scene = SceneManager.GetActiveScene().name ?? string.Empty; } catch { }
        if (string.IsNullOrEmpty(scene) || scene.IndexOf("Room_", StringComparison.OrdinalIgnoreCase) < 0) return;
        if (!string.Equals(scene, _awooRoomToken, StringComparison.Ordinal)) ResetAwooForCurrentRoom("room-change:" + scene);
    }

    private void ResetAwooForCurrentRoom(string reason)
    {
        string scene = string.Empty;
        try { scene = SceneManager.GetActiveScene().name ?? string.Empty; } catch { }
        _awooRoomToken = scene;
        _awooRAdRatio = 0.25f;
        _awooPitch = 1f;
        if (_hero != null && GetLevel(DariusEquipmentStarIds.Awoo) > 0)
            St_Darius_NoxianGuillotine.ApplyAwooCooldownOverrideToHero(_hero, true);
        DariusAwooAudioRuntime audio = _hero != null ? _hero.GetComponent<DariusAwooAudioRuntime>() : null;
        if (audio != null) audio.StopPlayback();
        DariusLog.Info("STAR-AWOO", "Room state reset reason=" + reason + " room=" + scene + " Rratio=0.25 pitch=1.0");
    }

    private void RestartAnimals()
    {
        if (_hero == null) return;
        DariusAwooAudioRuntime audio = _hero.GetComponent<DariusAwooAudioRuntime>();
        if (audio == null) audio = _hero.gameObject.AddComponent<DariusAwooAudioRuntime>();
        audio.Restart(_hero, _awooPitch);
    }

    private void NotifyEquipmentKillIfNeeded(Entity victim, string source)
    {
        if (victim == null || _hero == null) return;
        bool killed = victim.IsNullInactiveDeadOrKnockedOut() || victim.currentHealth <= 0f;
        if (killed) DariusConstellationRuntime.NotifyKill(_hero, victim, source);
    }

    private void Update()
    {
        if (!NetworkServer.active || _hero == null || _hero.Status == null) return;
        float hpBefore = _lastHealth;
        float hpNow = _hero.currentHealth;
        if (hpBefore < 0f) hpBefore = hpNow;
        if (hpNow + 0.01f < hpBefore) OnOwnerDamaged(hpBefore - hpNow);

        if (_shojinStacks > 0 && Time.time >= _shojinExpiresAt)
        {
            _shojinStacks = 0;
            _lastShojinSource = null;
        }
        TickDeathDance();
        TickBlackCleaver();
        TickMovement();
        RefreshBloodmail(false);
        RefreshYoumuu(false);
        CheckAwooRoom();

        // Capture after all self-heal/deferred-damage effects so their own corrections are not
        // mistaken for a second incoming hit next frame.
        _lastHealth = _hero.currentHealth;
    }

    private void OnDestroy()
    {
        if (_attackSubscribed && _hero != null)
        {
            try { _hero.ActorEvent_OnAttackHit -= OnAttackHit; } catch { }
        }
        _attackSubscribed = false;
        ClearBlackCleaver();
        RemoveSterakBarrier();
        if (_sterakBarrierRoutine != null) { try { StopCoroutine(_sterakBarrierRoutine); } catch { } }
        if (_bloodmailBonus != null && _hero != null && _hero.Status != null) { try { _hero.Status.RemoveStatBonus(_bloodmailBonus); } catch { } }
        if (_youmuuBonus != null && _hero != null && _hero.Status != null) { try { _hero.Status.RemoveStatBonus(_youmuuBonus); } catch { } }
        _bloodmailBonus = null;
        _youmuuBonus = null;
        _deathDance.Clear();
        _sunderedReady.Clear();
        _recentKills.Clear();
    }
}

// Optional local song player for the user-authored Awoo constellation. The Mod does not bundle
// copyrighted music. If the user puts assets/audio/animals.ogg or animals.wav in their own Mod
// folder, this component uses that exact local file. Missing audio is a silent, non-fatal state.
public sealed class DariusAwooAudioRuntime : MonoBehaviour
{
    private Hero _hero;
    private AudioSource _source;
    private AudioClip _clip;
    private bool _loading;
    private bool _missingLogged;
    private float _pendingPitch = 1f;

    public void Restart(Hero hero, float pitch)
    {
        _hero = hero;
        _pendingPitch = Mathf.Clamp(pitch, 0.5f, 2.0f);
        if (!IsLocalHero()) return;
        EnsureSource();
        if (_clip != null)
        {
            PlayNow();
            return;
        }
        if (_loading) return;
        string root = DariusModEnvironment.ResolveRoot();
        string wav = !string.IsNullOrEmpty(root) ? Path.Combine(root, "assets", "audio", "animals.wav") : null;
        string ogg = !string.IsNullOrEmpty(root) ? Path.Combine(root, "assets", "audio", "animals.ogg") : null;
        if (!string.IsNullOrEmpty(wav) && File.Exists(wav))
        {
            _clip = DariusMedia.Clip("animals");
            if (_clip != null) PlayNow();
            return;
        }
        if (!string.IsNullOrEmpty(ogg) && File.Exists(ogg))
        {
            StartCoroutine(LoadOgg(ogg));
            return;
        }
        if (!_missingLogged)
        {
            _missingLogged = true;
            DariusLog.Warn("STAR-AWOO-AUDIO", "Optional Animals audio is not bundled. Put a user-owned assets/audio/animals.ogg or animals.wav in the Darius Mod folder to enable this star's song playback.");
        }
    }

    public void StopPlayback()
    {
        if (_source != null) { try { _source.Stop(); } catch { } }
    }

    private bool IsLocalHero()
    {
        try { return _hero != null && DewPlayer.local != null && DewPlayer.local.hero == _hero; }
        catch { return true; }
    }

    private void EnsureSource()
    {
        if (_source != null) return;
        _source = gameObject.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.loop = false;
        _source.spatialBlend = 0f;
    }

    private IEnumerator LoadOgg(string path)
    {
        _loading = true;
        string uri;
        try { uri = new Uri(path).AbsoluteUri; }
        catch (Exception e)
        {
            _loading = false;
            DariusLog.Exception("STAR-AWOO-AUDIO", e, "Could not make URI for optional Animals OGG");
            yield break;
        }
        using (UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(uri, (UnityEngine.AudioType)14))
        {
            yield return req.SendWebRequest();
            _loading = false;
            if (!string.IsNullOrEmpty(req.error))
            {
                DariusLog.Error("STAR-AWOO-AUDIO", "Optional Animals OGG decode failed: " + req.error);
                yield break;
            }
            try { _clip = DownloadHandlerAudioClip.GetContent(req); }
            catch (Exception e) { DariusLog.Exception("STAR-AWOO-AUDIO", e, "Optional Animals OGG GetContent failed"); }
        }
        if (_clip != null) PlayNow();
    }

    private void PlayNow()
    {
        if (_clip == null || !IsLocalHero()) return;
        EnsureSource();
        _source.Stop();
        _source.clip = _clip;
        _source.pitch = _pendingPitch;
        _source.volume = Mathf.Clamp01(0.75f * DariusAudioSettingsRuntime.Multiplier(DariusAudioChannel.R));
        _source.time = 0f;
        _source.Play();
        DariusLog.DebugInfo("STAR-AWOO-AUDIO", "Restarted optional Animals audio pitch=" + _pendingPitch.ToString("0.0"));
    }
}
