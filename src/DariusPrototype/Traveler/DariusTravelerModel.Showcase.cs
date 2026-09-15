using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed partial class DariusTravelerModelInstance : MonoBehaviour
{
    private IEnumerator PlayIntroSequence()
    {
        _model.Play("Idle_In", false, true);
        yield return new WaitForSeconds(_model.GetClipLength("Idle_In", 0.5f));
        if (_model != null && !_deathLatched) _model.Play(IdleClip, true, true);
        _animationSequence = null;
    }

    private IEnumerator PlayRespawnSequence()
    {
        _model.Play("Respawn", false, true);
        yield return new WaitForSeconds(_model.GetClipLength("Respawn", 0.5f));
        if (_model != null && !_deathLatched) _model.Play(IdleClip, true, true);
        _animationSequence = null;
    }

    private IEnumerator PlayIdleVariant()
    {
        _model.Play(IdleVariantClip, false, true);
        yield return new WaitForSeconds(_model.GetClipLength(IdleVariantClip, 1f));
        if (_model != null && !_deathLatched) _model.Play(IdleClip, true, true);
        _animationSequence = null;
    }

    private IEnumerator PlayLobbyShowcase()
    {
        // Keep automatic lobby showcases conservative. Joke/Recall chains can depend on skin
        // props/VFX that are not present in the raw GLB, and long chained showcases made the lobby
        // appear permanently busy. Those clips remain packaged for explicit/manual use later.
        string[][] chains = _godKing
            ? new[]
            {
                new[] { "Taunt" },
                new[] { "Laugh" },
                new[] { "Dance_In", "Dance" },
                new[] { "Channel_Wndup", "Channel" },
            }
            : new[]
            {
                new[] { "Taunt" },
                new[] { "Laugh" },
                new[] { "Dance" },
                new[] { "Channel_Wndup", "Channel" },
            };

        string[] chain = chains[_previewShowcaseIndex++ % chains.Length];
        for (int i = 0; i < chain.Length; i++)
        {
            if (_model == null || !_lobbyPreview) break;
            string clip = chain[i];
            if (!_model.HasClip(clip)) continue;
            _model.Play(clip, false, true);
            yield return new WaitForSeconds(_model.GetClipLength(clip, 0.6f));
        }
        if (_model != null && _lobbyPreview) _model.PlayBase(IdleClip, true, true);
        _nextPreviewShowcaseAt = Time.time + 30.0f;
        _animationSequence = null;
    }

    private void StopAnimationSequence()
    {
        if (_animationSequence != null)
        {
            try { StopCoroutine(_animationSequence); } catch { }
            _animationSequence = null;
        }
        _wSwingSequence = false;
        CancelAttackFacing("action interrupted");
        if (_godKing && _model != null)
        {
            try { _model.SetMaterialVisible("Wolf_Mat", false); } catch { }
        }
    }

    private void OnDestroy()
    {
        StopAnimationSequence();
        if (_model != null)
        {
            try { _model.Dispose(); } catch { }
            _model = null;
        }
    }
}
