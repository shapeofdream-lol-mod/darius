using UnityEngine;

public sealed class DariusHemorrhageHud : MonoBehaviour
{
    private Hero _owner;
    private DariusHemorrhageRuntime _passive;
    private UI_HealthBar_Simple _healthBar;
    private float _nextHealthBarLookupAt;

    private void Awake() { _owner = GetComponent<Hero>(); _passive = GetComponent<DariusHemorrhageRuntime>(); }

    private void OnGUI()
    {
        if (Event.current != null && Event.current.type != EventType.Repaint) return;
        if (!ShouldDraw()) return;
        int stacks; float mightRatio; ReadSyncedState(out stacks, out mightRatio);
        Rect healthRect;
        if (!TryGetWorldHealthBarRect(out healthRect)) return;
        float width = healthRect.width * 0.96f;
        float height = Mathf.Clamp(healthRect.height * 0.38f, 6f, 9f);
        float left = healthRect.center.x - width * 0.5f;
        float top = healthRect.yMax - 31f;
        Color old = GUI.color;
        GUI.color = new Color(0.09f, 0.015f, 0.02f, 0.94f); GUI.DrawTexture(new Rect(left, top, width, height), Texture2D.whiteTexture);
        if (mightRatio > 0f)
        {
            GUI.color = new Color(0.92f, 0.08f, 0.10f, 1f);
            GUI.DrawTexture(new Rect(left + 2f, top + 2f, (width - 4f) * mightRatio, height - 4f), Texture2D.whiteTexture);
        }
        else
        {
            int max = Mathf.Max(1, _passive.currentMaxStacks);
            const float gap = 3f;
            float cell = (width - gap * (max - 1)) / max;
            for (int i = 0; i < max; i++)
            {
                GUI.color = i < stacks ? new Color(0.78f, 0.04f, 0.07f, 0.98f) : new Color(0.25f, 0.07f, 0.08f, 0.82f);
                GUI.DrawTexture(new Rect(left + i * (cell + gap), top, cell, height), Texture2D.whiteTexture);
            }
        }
        GUI.color = old;
    }

    private bool TryGetWorldHealthBarRect(out Rect rect)
    {
        rect = default(Rect);
        if ((_healthBar == null || _healthBar.target != _owner) && Time.unscaledTime >= _nextHealthBarLookupAt)
        {
            _nextHealthBarLookupAt = Time.unscaledTime + 0.5f; _healthBar = null;
            UI_HealthBar_Simple[] bars = Object.FindObjectsOfType<UI_HealthBar_Simple>();
            for (int i = 0; i < bars.Length; i++) if (bars[i] != null && bars[i].target == _owner) { _healthBar = bars[i]; break; }
        }
        if (_healthBar != null && _healthBar.hpBgTransform != null)
        {
            RectTransform visibleBar = _healthBar.hpBgTransform;
            try
            {
                var field = _healthBar.GetType().GetField("healthFill");
                Component fill = field != null ? field.GetValue(_healthBar) as Component : null;
                RectTransform fillRect = fill != null ? fill.transform as RectTransform : null;
                if (fillRect != null) visibleBar = fillRect;
            }
            catch { }
            Vector3[] corners = new Vector3[4]; visibleBar.GetWorldCorners(corners);
            Vector2 a = corners[0];
            Vector2 b = corners[2];
            float nativeWidth = Mathf.Abs(b.x - a.x);
            if (nativeWidth > 8f)
            {
                float width = Mathf.Clamp(nativeWidth, 96f, 180f);
                float nativeHeight = Mathf.Abs(b.y - a.y);
                rect = new Rect((a.x + b.x - width) * 0.5f, Screen.height - Mathf.Max(a.y, b.y), width, Mathf.Max(8f, nativeHeight));
                return true;
            }
        }
        try
        {
            Camera cam = Dew.mainCamera != null ? Dew.mainCamera : Camera.main;
            if (cam == null || _owner.Visual == null) return false;
            Vector3 world = _owner.Visual.model != null && _owner.Visual.model.healthBarPosition != null ? _owner.Visual.model.healthBarPosition.position : _owner.Visual.GetAbovePosition();
            Vector3 screen = cam.WorldToScreenPoint(world);
            if (screen.z <= 0f || screen.x < -20f || screen.x > Screen.width + 20f || screen.y < -20f || screen.y > Screen.height + 20f) return false;
            float width = Mathf.Clamp(Screen.width * 0.072f, 112f, 150f);
            rect = new Rect(screen.x - width * 0.5f, Screen.height - screen.y - 5f, width, 16f); return true;
        }
        catch { return false; }
    }

    private void ReadSyncedState(out int stacks, out float mightRatio)
    {
        stacks = 0; mightRatio = 0f;
        try
        {
            Color c = _owner.Skill.Identity.specialOverlayColor;
            if (Mathf.Abs(c.b - 0.619f) < 0.01f)
            {
                stacks = Mathf.Clamp(Mathf.RoundToInt(c.r * _passive.currentMaxStacks), 0, _passive.currentMaxStacks);
                mightRatio = Mathf.Clamp01(c.g); return;
            }
        }
        catch { }
    }

    private bool ShouldDraw()
    {
        if (_owner == null) _owner = GetComponent<Hero>(); if (_passive == null) _passive = GetComponent<DariusHemorrhageRuntime>();
        if (_owner == null || _passive == null || !_passive.isEnabled) return false;
        if (IsCoveredByMenu()) return false;
        try { return DewPlayer.local != null && DewPlayer.local.hero == _owner; } catch { return false; }
    }

    private static bool IsCoveredByMenu()
    {
        try
        {
            InGameUIManager ui = InGameUIManager.softInstance;
            if (ui == null || !ui.IsState("Playing") || ui.disablePlayingInput || ui.isScoreboardDisplayed || ui.isWorldDisplayed != WorldDisplayStatus.None) return true;
            GlobalUIManager global = ManagerBase<GlobalUIManager>.softInstance;
            return global != null && global.currentMenuView != null && global.currentMenuView.IsShowing();
        }
        catch { return true; }
    }
}
