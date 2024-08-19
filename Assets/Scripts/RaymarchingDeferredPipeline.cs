using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public class RaymarchingDeferredPipeline : RenderPipeline {
    RaymarchingDeferredPipelineAsset pipelineAsset;
    CullingResults cullingResults;
    ScriptableCullingParameters cullingParameters;

    // Pre-allocate array for use with SetRenderTarget.
    RenderTargetIdentifier[] gbufferRTIDs = new RenderTargetIdentifier[2];

    const int maxVisibleLights = 4;
	static int visibleLightColorsId = Shader.PropertyToID("_VisibleLightColors");
	static int visibleLightDirectionsOrPositionsId = Shader.PropertyToID("_VisibleLightDirectionsOrPositions");

    private static int _kernel;

    public RaymarchingDeferredPipeline(RaymarchingDeferredPipelineAsset _pipelineAsset) {
        pipelineAsset = _pipelineAsset;
        GraphicsSettings.lightsUseLinearIntensity = true;
        _kernel = 0;
    }

    protected override void Render(ScriptableRenderContext renderContext, Camera[] cameras) {
        foreach (var camera in cameras) {
            Render(renderContext, camera);
        }
    }

    private void Render(ScriptableRenderContext renderContext, Camera camera) {
        #if UNITY_EDITOR
            if (camera.cameraType == CameraType.SceneView) {
                ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
            }
        #endif

        // Culling
        if (camera.TryGetCullingParameters(out cullingParameters)) {
            cullingResults = renderContext.Cull(ref cullingParameters);
        }
        else{ return; }
        renderContext.SetupCameraProperties(camera);

        // Declare RT buffers for deferred rendering
        var gbuffer0RT = Shader.PropertyToID("_GBuffer0"); // Albedo
        var gbuffer0RTID = new RenderTargetIdentifier(gbuffer0RT);
        var gbuffer1RT = Shader.PropertyToID("_GBuffer1"); // Normals
        var gbuffer1RTID = new RenderTargetIdentifier(gbuffer1RT);
        var rMShadowsRT = Shader.PropertyToID("_RMShadows"); // RaymarchedShadows
        var rMShadowsRTID = new RenderTargetIdentifier(rMShadowsRT);
        var depthRT = Shader.PropertyToID("_CameraDepthTexture"); // Depth
        var depthRTID = new RenderTargetIdentifier(depthRT);
        var tempDepthRT = Shader.PropertyToID("_TempDepthTexture"); // Temporary depth for raymarching
        var tempDepthRTID = new RenderTargetIdentifier(tempDepthRT);
        var colorFogRT = Shader.PropertyToID("_ColorFogRT"); // Final color
        var colorFogRTID = new RenderTargetIdentifier(colorFogRT);
        var colorRT = Shader.PropertyToID("_ColorRT"); // Final color
        var colorRTID = new RenderTargetIdentifier(colorRT);

        {   // Create the RTs
            var cmd = CommandBufferPool.Get("Set-up Render Targets");
            cmd.GetTemporaryRT(gbuffer0RT, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear, 1, true);
            cmd.GetTemporaryRT(gbuffer1RT, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGB2101010, RenderTextureReadWrite.Linear, 1, true);
            cmd.GetTemporaryRT(rMShadowsRT, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.R8, RenderTextureReadWrite.Linear, 1, true);
            cmd.GetTemporaryRT(tempDepthRT, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear, 1, true);
            cmd.GetTemporaryRT(colorFogRT, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear, 1, true);
            cmd.GetTemporaryRT(colorRT, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            cmd.GetTemporaryRT(depthRT, camera.pixelWidth, camera.pixelHeight, 24, FilterMode.Point, RenderTextureFormat.Depth);

            gbufferRTIDs[0] = gbuffer0RTID;
            gbufferRTIDs[1] = gbuffer1RTID;

            // Clear the RTs
            cmd.SetRenderTarget(tempDepthRT, depthRTID);
            cmd.ClearRenderTarget(true, true, Color.black);
            cmd.SetRenderTarget(gbufferRTIDs, depthRTID);
            cmd.ClearRenderTarget(true, true, Color.black);
            renderContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        // Draw Opaques to RTs
        var sortingSettings = new SortingSettings(camera) {criteria = SortingCriteria.CommonOpaque};
        var drawingSettings = new DrawingSettings(new ShaderTagId("GBuffer"), sortingSettings);   
        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
        renderContext.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);

        // Raymarch on RTs and temporary depth ( Compute shader cannot write to depth )
        List<Primitive> allPrimitives = new List<Primitive>(Object.FindObjectsOfType<Primitive>());

        if (pipelineAsset.compute != null && allPrimitives.Count != 0)
        {
            var cmdCompute = CommandBufferPool.Get("Sphere tracing");
            cmdCompute.SetComputeTextureParam(pipelineAsset.compute, _kernel, gbuffer0RT, gbuffer0RTID);
            cmdCompute.SetComputeTextureParam(pipelineAsset.compute, _kernel, gbuffer1RT, gbuffer1RTID);
            cmdCompute.SetComputeTextureParam(pipelineAsset.compute, _kernel, rMShadowsRT, rMShadowsRTID);
            cmdCompute.SetComputeTextureParam(pipelineAsset.compute, _kernel, depthRT, depthRTID);

            var camToWorldID = Shader.PropertyToID("_CameraToWorld");
            cmdCompute.SetComputeMatrixParam(pipelineAsset.compute,
                camToWorldID, camera.cameraToWorldMatrix);
            var camInvProjectID = Shader.PropertyToID("_CameraInverseProjection");
            cmdCompute.SetComputeMatrixParam(pipelineAsset.compute,
                camInvProjectID, camera.projectionMatrix.inverse);
            var wordToCameraID = Shader.PropertyToID("_WorldToCamera");
            cmdCompute.SetComputeMatrixParam(pipelineAsset.compute,
                wordToCameraID, camera.worldToCameraMatrix);
            var camViewDirID = Shader.PropertyToID("_CameraViewDirection");
            cmdCompute.SetComputeVectorParam(pipelineAsset.compute,
                camViewDirID, camera.transform.forward);
            var lightDirID = Shader.PropertyToID("_LightDir");
            cmdCompute.SetComputeVectorParam(pipelineAsset.compute,
                lightDirID, cullingResults.visibleLights[0].localToWorldMatrix.GetColumn(2));
            var lightPosID = Shader.PropertyToID("_LightPos");
            cmdCompute.SetComputeVectorParam(pipelineAsset.compute,
                lightPosID, cullingResults.visibleLights[0].light.transform.position);
            var shadowSettingsID = Shader.PropertyToID("_ShadowParams");
            cmdCompute.SetComputeVectorParam(pipelineAsset.compute,
                shadowSettingsID, new Vector4(pipelineAsset.minShadowDistance, pipelineAsset.maxShadowDistance, pipelineAsset.softShadowsFactor,pipelineAsset.shadowIntensity));

            var epsilonID = Shader.PropertyToID("_Epsilon");
            cmdCompute.SetComputeFloatParam(pipelineAsset.compute,epsilonID, pipelineAsset.epsilon);

            allPrimitives.Sort((a, b) => a.operation.CompareTo(b.operation));

            List<Primitive> orderedPrimitives = new List<Primitive>();

            for (int i = 0; i < allPrimitives.Count; i++)
            {
                // Add top-level shapes (those without a parent)
                if (allPrimitives[i].transform.parent == null)
                {

                    Transform parentPrimitive = allPrimitives[i].transform;
                    orderedPrimitives.Add(allPrimitives[i]);
                    allPrimitives[i].numChildren = parentPrimitive.childCount;
                    // Add all children of the shape (nested children not supported currently)
                    for (int j = 0; j < parentPrimitive.childCount; j++)
                    {
                        if (parentPrimitive.GetChild(j).GetComponent<Primitive>() != null)
                        {
                            orderedPrimitives.Add(parentPrimitive.GetChild(j).GetComponent<Primitive>());
                            orderedPrimitives[orderedPrimitives.Count - 1].numChildren = 0;
                        }
                    }
                }
            }

            PrimitiveData[] primData = new PrimitiveData[orderedPrimitives.Count];
            for (int i = 0; i < orderedPrimitives.Count; i++)
            {
                var p = orderedPrimitives[i];
                Vector3 col = new Vector3(p.colour.r, p.colour.g, p.colour.b);
                Matrix4x4 rot = Matrix4x4.Rotate(Quaternion.Euler(p.Rotation));
                primData[i] = new PrimitiveData()
                {
                    position = p.Position,
                    scale = p.Scale,
                    rotation = rot,
                    colour = col,
                    primitiveType = (int)p.primitiveType,
                    operation = (int)p.operation,
                    blendStrength = p.blendStrength * 3,
                    numChildren = p.numChildren
                };
            }
            
            ComputeBuffer shapeBuffer = new ComputeBuffer(primData.Length, PrimitiveData.GetSize());
            shapeBuffer.SetData(primData);

            var primitiveDataID = Shader.PropertyToID("primitives");
            cmdCompute.SetComputeBufferParam(pipelineAsset.compute, _kernel, primitiveDataID, shapeBuffer);
            var primitiveNumberID = Shader.PropertyToID("primitiveNumber");
            cmdCompute.SetComputeIntParam(pipelineAsset.compute, primitiveNumberID, primData.Length); ;

            cmdCompute.DispatchCompute(pipelineAsset.compute, _kernel, camera.pixelWidth / 8 + 1, camera.pixelHeight / 8 + 1, 1);
            renderContext.ExecuteCommandBuffer(cmdCompute);
            cmdCompute.Release();
        }


        {   // Copy raymarching temporary depth texture to the actual depth
            var cmd = CommandBufferPool.Get("Raymarch copy Depth");
            cmd.Blit(tempDepthRTID, depthRT, pipelineAsset.copyRayDepthMaterial);
            cmd.SetRenderTarget(gbufferRTIDs, depthRTID);
            renderContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        {   // Light buffer setup
            var visibleLightColors = new Vector4[maxVisibleLights];
            var visibleLightDirectionsOrPositions = new Vector4[maxVisibleLights];

            int realMaxLights = Mathf.Min(cullingResults.visibleLights.Length, maxVisibleLights);
            for (int i = 0; i < realMaxLights; i++) {
                VisibleLight light = cullingResults.visibleLights[i];
                visibleLightColors[i] = light.finalColor; // finalcolor = lightcolor * intensity
                if (light.lightType == LightType.Directional) {
                    Vector4 v = light.localToWorldMatrix.GetColumn(2);
                    v.x = -v.x;
                    v.y = -v.y;
                    v.z = -v.z;
                    visibleLightDirectionsOrPositions[i] = v;
                }
                else { // Assume light is a point light
                    visibleLightDirectionsOrPositions[i] = light.localToWorldMatrix.GetColumn(3);
                }
            }

            var cmd = CommandBufferPool.Get("Set-up Light Buffer");

            cmd.SetGlobalVectorArray(visibleLightColorsId, visibleLightColors);
            cmd.SetGlobalVectorArray(visibleLightDirectionsOrPositionsId,
                                               visibleLightDirectionsOrPositions);
            renderContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        {   // Deferred shading
            var cmd = CommandBufferPool.Get("Deferred");

            cmd.Blit(null, colorFogRTID, pipelineAsset.deferredMaterial);
            // Switch back to the correct RT
            cmd.SetRenderTarget(colorFogRTID, depthRTID);
            renderContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        // Skybox
        renderContext.DrawSkybox(camera);

        {   //FOG
            var cmd = CommandBufferPool.Get("Fog");
            cmd.SetGlobalFloat("_FogDensity", pipelineAsset.fogDensity);
            cmd.SetGlobalColor("_FogColor", pipelineAsset.fogColor);
            cmd.Blit(null, colorRTID, pipelineAsset.fogMaterial);
            cmd.SetRenderTarget(colorRTID, depthRTID);
            renderContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }


        // Forward rendering transparents objects
        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings = new DrawingSettings(new ShaderTagId("Forward"), sortingSettings);
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;
        renderContext.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);


        {   // Copy depth
            var cmd = CommandBufferPool.Get("Copy Depth");
            cmd.Blit(BuiltinRenderTextureType.CameraTarget, BuiltinRenderTextureType.CameraTarget, pipelineAsset.copyDepthMaterial);
            renderContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        {   // Final blit to camera and cleanup RTs
            var cmd = CommandBufferPool.Get("Final Blit");
            cmd.Blit(colorRT, BuiltinRenderTextureType.CameraTarget);
            cmd.ReleaseTemporaryRT(gbuffer0RT);
            cmd.ReleaseTemporaryRT(gbuffer1RT);
            cmd.ReleaseTemporaryRT(rMShadowsRT);
            cmd.ReleaseTemporaryRT(tempDepthRT);
            cmd.ReleaseTemporaryRT(colorRT);
            cmd.ReleaseTemporaryRT(depthRT);
            cmd.ReleaseTemporaryRT(colorFogRT);
            renderContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        // Draw Gizmos
        if (camera.cameraType == CameraType.SceneView)
        {
            renderContext.DrawGizmos(camera, GizmoSubset.PreImageEffects);
            renderContext.DrawGizmos(camera, GizmoSubset.PostImageEffects);
        }

        renderContext.Submit();
    }
    struct PrimitiveData
    {
        public Vector3 position;
        public Vector3 scale;
        public Matrix4x4 rotation;
        public Vector3 colour;
        public int primitiveType;
        public int operation;
        public float blendStrength;
        public int numChildren;

        public static int GetSize()
        {
            return sizeof(float) * 26 + sizeof(int) * 3;
        }
    }
}