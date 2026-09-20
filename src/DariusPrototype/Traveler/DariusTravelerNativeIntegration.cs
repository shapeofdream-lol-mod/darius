using System;
using UnityEngine;

public sealed class DariusOfficialEntityModelMarker : MonoBehaviour
{
    public string variantKey;
}

// Runtime-created Hero/Skin resources remain necessary because the stock mod loader does not
// extend the game's Addressables catalog with custom Hero/Skin GUIDs. Model playback itself uses
// the fresh native EntityModel + stock SoD EntityAnimation lifecycle.
public sealed class Hero_Darius : Hero
{
    public override void OnModelLoaded()
    {
        base.OnModelLoaded();
        try
        {
            EntityModel loadedModel = Visual != null ? Visual.model : null;
            DariusOfficialEntityModelMarker marker =
                loadedModel != null ? loadedModel.GetComponent<DariusOfficialEntityModelMarker>() : null;
            if (loadedModel == null || marker == null)
            {
                DariusLog.Error("TRAVELER-MODEL",
                    "Hero_Darius loaded without the required fresh EntityModel marker; presentation binding skipped.");
                return;
            }

            DariusBasicAttackVisualRuntime attackVisual = GetComponent<DariusBasicAttackVisualRuntime>();
            if (attackVisual == null) attackVisual = gameObject.AddComponent<DariusBasicAttackVisualRuntime>();
            attackVisual.Bind(this);

            DariusOfficialActionRuntime action = loadedModel.GetComponent<DariusOfficialActionRuntime>();
            if (action == null)
            {
                DariusLog.Error("OFFICIAL-ACTION",
                    "Fresh Darius EntityModel has no DariusOfficialActionRuntime variant=" + marker.variantKey);
            }
            else
            {
                action.Bind(this);
            }

            EntityAnimation animation = GetComponent<EntityAnimation>();
            DariusLog.Info("TRAVELER-MODEL",
                "Hero_Darius.OnModelLoaded fresh=True" +
                " variant=" + marker.variantKey +
                " entityModel=" + loadedModel.name +
                " initialized=" + loadedModel.isInitialized +
                " animator=" + (animation != null && animation.animator != null
                    ? animation.animator.gameObject.name
                    : "<null>") +
                " actionOverlay=" + (action != null && action.IsReady) +
                " attackVisual=" + (attackVisual != null));

        }
        catch (Exception e)
        {
            DariusLog.Exception("TRAVELER-MODEL", e, "Hero_Darius.OnModelLoaded fresh-model bind failed");
        }
    }
}
