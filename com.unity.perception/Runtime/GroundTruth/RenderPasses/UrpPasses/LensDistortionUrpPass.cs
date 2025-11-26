#if URP_PRESENT
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEngine.Perception.GroundTruth
{
    class LensDistortionUrpPass : ScriptableRenderPass
    {
        public LensDistortionCrossPipelinePass lensDistortionCrossPipelinePass;

        public LensDistortionUrpPass(Camera camera, RenderTexture targetTexture)
        {
            lensDistortionCrossPipelinePass = new LensDistortionCrossPipelinePass(camera, targetTexture);
#if URP_17_OR_NEWER
            // Unity 6+: Use RTHandle overload
            ConfigureTarget(RTHandles.Alloc(targetTexture));
#else
            ConfigureTarget(targetTexture, targetTexture.depthBuffer);
#endif
            lensDistortionCrossPipelinePass.Setup();
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var commandBuffer = CommandBufferPool.Get(nameof(SemanticSegmentationUrpPass));
            lensDistortionCrossPipelinePass.Execute(context, commandBuffer, renderingData.cameraData.camera, renderingData.cullResults);
            CommandBufferPool.Release(commandBuffer);
        }

        public void Cleanup()
        {
            lensDistortionCrossPipelinePass.Cleanup();
        }
    }
}
#endif
