using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Mirror;
using UnityEngine;

public sealed partial class DariusGlbRuntimeModel
{
    private void BuildNodes()
    {
        JArray nodes = (JArray)_json["nodes"];
        _nodes = new Transform[nodes.Count];
        _basePos = new Vector3[nodes.Count];
        _baseRot = new Quaternion[nodes.Count];
        _baseScale = new Vector3[nodes.Count];

        for (int i = 0; i < nodes.Count; i++)
        {
            JObject n = (JObject)nodes[i];
            GameObject go = new GameObject((string)n["name"] ?? ("Node_" + i));
            _nodes[i] = go.transform;
            _nodes[i].SetParent(root.transform, false);
            Vector3 p = ReadVec3(n["translation"], Vector3.zero);
            Quaternion r = ReadQuat(n["rotation"], Quaternion.identity);
            Vector3 s = ReadVec3(n["scale"], Vector3.one);
            _nodes[i].localPosition = p;
            _nodes[i].localRotation = r;
            _nodes[i].localScale = s;
            _basePos[i] = p;
            _baseRot[i] = r;
            _baseScale[i] = s;
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            JArray children = nodes[i]["children"] as JArray;
            if (children == null) continue;
            for (int j = 0; j < children.Count; j++)
            {
                int child = (int)children[j];
                if (child >= 0 && child < _nodes.Length)
                    _nodes[child].SetParent(_nodes[i], false);
            }
        }
    }
}