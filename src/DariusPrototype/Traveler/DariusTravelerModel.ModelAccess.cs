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
    public Transform GetAnchor(string nodeName)
    {
        return _model != null ? _model.FindNode(nodeName) : null;
    }

    public GameObject CreateSubmeshOverlay(uint sourceMaterialHash, Material overlayMaterial)
    {
        return _model != null ? _model.CreateSubmeshOverlay(sourceMaterialHash, overlayMaterial) : null;
    }

    public GameObject CreateFullMeshOverlay(Material overlayMaterial, string label)
    {
        return _model != null ? _model.CreateFullMeshOverlay(overlayMaterial, label) : null;
    }

    public bool SetGodKingWolfVisible(bool visible)
    {
        if (!_godKing || _model == null) return false;
        bool ok = _model.SetMaterialVisible("Wolf_Mat", visible);
        DariusLog.DebugInfo("GODKING-WOLF", "Wolf_Mat visibility=" + visible + " ok=" + ok + " animation=" + (_model.currentAnimation ?? "<none>"));
        return ok;
    }
}