using UnityEngine;

[ExecuteAlways]
public class MorphController : MonoBehaviour
{
    [Range(0f, 3f)]
    public float morphState = 0f;

    private SkinnedMeshRenderer smr;

    void Awake()
    {
        smr = GetComponent<SkinnedMeshRenderer>();
    }

    void Update()
    {
        if (smr == null) smr = GetComponent<SkinnedMeshRenderer>();
        if (smr == null) return;

        // Reset all weights
        for (int i = 0; i < smr.sharedMesh.blendShapeCount; i++)
        {
            smr.SetBlendShapeWeight(i, 0f);
        }

        // Logic for states:
        // 0: Triangle (Base)
        // 1: Hexagon (Index 1)
        // 2: Sphere (Index 2)
        // 3: Square (Index 3)
        // We handle [Key 1] (Index 0) as part of the triangle if needed, but assuming base is tri.

        if (morphState <= 1f)
        {
            // Transition from Triangle (0) to Hexagon (1)
            smr.SetBlendShapeWeight(1, morphState * 100f);
        }
        else if (morphState <= 2f)
        {
            // Transition from Hexagon (1) to Sphere (2)
            float t = morphState - 1f;
            smr.SetBlendShapeWeight(1, (1f - t) * 100f);
            smr.SetBlendShapeWeight(2, t * 100f);
        }
        else if (morphState <= 3f)
        {
            // Transition from Sphere (2) to Square (3)
            float t = morphState - 2f;
            smr.SetBlendShapeWeight(2, (1f - t) * 100f);
            smr.SetBlendShapeWeight(3, t * 100f);
        }
    }
}
