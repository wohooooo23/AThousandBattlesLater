using UnityEngine;

/// <summary>Releases meshes/materials created for transient sector effects, including interrupted attacks.</summary>
public sealed class OwnedCombatMesh : MonoBehaviour
{
    private Mesh mesh;
    private Material material;
    public void Initialize(Mesh ownedMesh, Material ownedMaterial) { mesh = ownedMesh; material = ownedMaterial; }
    private void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
        if (material != null) Destroy(material);
    }
}
