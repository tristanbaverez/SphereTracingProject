using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/RaymarchingDeferredPipeline")]
public class RaymarchingDeferredPipelineAsset : RenderPipelineAsset {

    public Material copyDepthMaterial = null;

    public Material copyRayDepthMaterial = null;

    public Material deferredMaterial = null;

    public Material fogMaterial = null;

    public float fogDensity = 0.05f;
    public Color fogColor = Color.gray;

    [Range(0.001f, 100f)]
    public float maxShadowDistance = 80f;
    [Range(0.0001f, 1f)]
    public float minShadowDistance = 0.1f;
    [Range(1f, 64f)]
    public float softShadowsFactor = 16f;
    [Range(0f, 1f)]
    public float shadowIntensity = .2f;
    [Range(0.00001f, 1f)]
    public float epsilon = 0.001f;

    public ComputeShader compute = null;
    protected override RenderPipeline CreatePipeline () {
        return new RaymarchingDeferredPipeline(this);
    }
}