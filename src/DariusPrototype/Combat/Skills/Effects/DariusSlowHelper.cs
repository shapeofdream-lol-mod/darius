using System;
using System.Collections;
using UnityEngine;

public static class DariusSlowHelper
{
    public static void Apply(Entity target, float strength, float duration, string source = null)
    {
        if (target == null || target.Status == null)
        {
            DariusLog.Warn("SLOW", "Apply skipped because target/status is null. source=" + (source ?? "<unknown>"));
            return;
        }

        try
        {
            DariusSlowRuntime runtime = target.GetComponent<DariusSlowRuntime>();
            if (runtime == null) runtime = target.gameObject.AddComponent<DariusSlowRuntime>();
            runtime.Apply(target, strength, duration, source);
        }
        catch (Exception e)
        {
            DariusLog.Exception("SLOW", e, "Failed to apply slow. source=" + (source ?? "<unknown>"));
        }
    }
}

public sealed class DariusSlowRuntime : MonoBehaviour
{
    private Entity _target;
    private StatBonus _bonus;
    private Coroutine _routine;
    private string _source;

    public void Apply(Entity target, float strength, float duration, string source)
    {
        _target = target;
        _source = source ?? "<unknown>";
        if (_routine != null)
        {
            StopCoroutine(_routine);
            DariusLog.DebugInfo("SLOW", "Refreshing existing slow target=" + DariusLog.EntityLabel(target) + " source=" + _source);
        }
        _routine = StartCoroutine(Run(Mathf.Abs(strength), duration));
    }

    private IEnumerator Run(float strength, float duration)
    {
        RemoveBonus();
        _bonus = new StatBonus();
        _bonus.movementSpeedPercentage = -strength * 100f;
        _target.Status.AddStatBonus(_bonus);
        DariusLog.Info("SLOW", "Applied " + (strength * 100f).ToString("0.#") + "% slow for " + duration.ToString("0.###") +
            "s target=" + DariusLog.EntityLabel(_target) + " source=" + _source);
        yield return new WaitForSeconds(duration);
        RemoveBonus();
        DariusLog.Info("SLOW", "Expired target=" + DariusLog.EntityLabel(_target) + " source=" + _source);
        _routine = null;
    }

    private void OnDestroy()
    {
        DariusLog.DebugInfo("SLOW", "Runtime destroyed target=" + DariusLog.EntityLabel(_target) + " source=" + _source);
        RemoveBonus();
    }

    private void RemoveBonus()
    {
        if (_bonus != null && _target != null && _target.Status != null)
        {
            try { _target.Status.RemoveStatBonus(_bonus); }
            catch (Exception e) { DariusLog.Exception("SLOW", e, "RemoveStatBonus failed source=" + _source); }
        }
        _bonus = null;
    }
}
