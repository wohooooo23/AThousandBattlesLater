using UnityEngine;

/// <summary>Ownership root for generated actors, shots and warnings.</summary>
public sealed class GeneratedMapContent : MonoBehaviour
{
    public static void Adopt(Transform source, GameObject child)
    {
        var content = source != null ? source.GetComponentInParent<GeneratedMapContent>() : null;
        if (content == null && WfcDungeonGenerator.Active != null && source == WfcDungeonGenerator.Active.hero.transform)
            content = WfcDungeonGenerator.Active.Content;
        if (content != null) child.transform.SetParent(content.transform, true);
    }
}
