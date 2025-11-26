#if URP_PRESENT
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEngine.Perception.GroundTruth
{
    class InstanceSegmentationUrpPass : ScriptableRenderPass
    {
        InstanceSegmentationCrossPipelinePass m_InstanceSegmentationPass;

        public InstanceSegmentationUrpPass(Camera camera, RenderTexture targetTexture)
        {
            m_InstanceSegmentationPass = new InstanceSegmentationCrossPipelinePass(camera);
#if URP_17_OR_NEWER
            // Unity 6+: Use RTHandle overload
            ConfigureTarget(RTHandles.Alloc(targetTexture));
#else
            ConfigureTarget(targetTexture, targetTexture.depthBuffer);
#endif
            ConfigureClear(ClearFlag.None, Color.black);
            m_InstanceSegmentationPass.Setup();
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var commandBuffer = CommandBufferPool.Get(nameof(InstanceSegmentationUrpPass));
            m_InstanceSegmentationPass.Execute(context, commandBuffer, renderingData.cameraData.camera, renderingData.cullResults);
            CommandBufferPool.Release(commandBuffer);
        }

        public void Cleanup()
        {
            m_InstanceSegmentationPass.Cleanup();
        }
    }
}
#endif
