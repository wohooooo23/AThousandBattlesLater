using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Removes obsolete hand-placed navigation objects from campaign scenes.</summary>
public static class BossNavigationMigration
{
    private static readonly string[] CampaignScenes =
    {
        "Assets/Scenes/stage1_full.unity",
        "Assets/Scenes/stage2_full.unity"
    };

    [MenuItem("Tools/Boss/Remove Authored Navigation Nodes From Campaign")]
    public static void RemoveCampaignNodes()
    {
        foreach (string path in CampaignScenes)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int removed = 0;
            foreach (EnemyNavigationNode node in Object.FindObjectsByType<EnemyNavigationNode>(
                         FindObjectsInactive.Include))
            {
                if (node.gameObject.scene != scene)
                    continue;
                Object.DestroyImmediate(node.gameObject);
                removed++;
            }
            foreach (GameObject root in scene.GetRootGameObjects())
                RemoveEmptyNodeContainers(root.transform);
            if (!EditorSceneManager.SaveScene(scene, path))
                throw new System.InvalidOperationException("Could not save " + path);
            Debug.Log(path + ": removed " + removed + " authored Boss navigation nodes.");
        }
    }

    private static void RemoveEmptyNodeContainers(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            RemoveEmptyNodeContainers(parent.GetChild(i));
        if (parent.childCount == 0 &&
            (parent.name == "Boss Arena Navigation Nodes" || parent.name == "Enemy Navigation Nodes"))
            Object.DestroyImmediate(parent.gameObject);
    }
}
