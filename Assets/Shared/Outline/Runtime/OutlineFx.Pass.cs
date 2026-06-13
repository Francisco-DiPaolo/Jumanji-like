using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace OutlineFx
{
    public partial class OutlineFxFeature
    {
        private static readonly int s_Alpha    = Shader.PropertyToID("_Alpha");
        private static readonly int s_MainTex  = Shader.PropertyToID("_MainTex");
        private static readonly int s_Step     = Shader.PropertyToID("_Step");
        private static readonly int s_Color    = Shader.PropertyToID("_Color");
        private static readonly int s_Solid    = Shader.PropertyToID("_Solid");
        private static readonly int s_AlphaTex = Shader.PropertyToID("_AlphaTex");
        private static readonly int s_AlphaTO  = Shader.PropertyToID("_AlphaTO");

        private class Pass : ScriptableRenderPass
        {
            public OutlineFxFeature _owner;

            public void Init()
            {
                renderPassEvent = _owner._event;
            }

            private class PassData
            {
                public OutlineFxFeature owner;
                public TextureHandle    buffer;
                public TextureHandle    cameraColor;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_registered.Count == 0 || _owner._outlineMat == null)
                    return;

                var cameraData   = frameData.Get<UniversalCameraData>();
                var resourceData = frameData.Get<UniversalResourceData>();

                var desc = cameraData.cameraTargetDescriptor;
                desc.colorFormat     = RenderTextureFormat.ARGB32;
                desc.depthBufferBits = 0;
                desc.msaaSamples     = 1;

                TextureHandle bufferHandle = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph, desc, "_OutlineBuffer", false, FilterMode.Bilinear);

                using (var builder = renderGraph.AddUnsafePass<PassData>("OutlineFx", out var passData))
                {
                    passData.owner       = _owner;
                    passData.buffer      = bufferHandle;
                    passData.cameraColor = resourceData.activeColorTexture;

                    builder.UseTexture(bufferHandle, AccessFlags.ReadWrite);
                    builder.UseTexture(resourceData.activeColorTexture, AccessFlags.ReadWrite);
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((PassData data, UnsafeGraphContext ctx) =>
                    {
                        var cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);
                        var mat = data.owner._outlineMat;

                        mat.SetFloat(s_Alpha, data.owner._alphaCutout);
                        mat.SetFloat(s_Solid, data.owner._solid);

                        if (data.owner._solidMask._enabled)
                        {
                            var sm        = data.owner._solidMask;
                            var aspectTex = sm._pattern.width / (float)sm._pattern.height;
                            var xPeriod   = sm._velocity.x == 0 ? 1f : 1f / (sm._velocity.x / 1000f);
                            var yPeriod   = sm._velocity.y == 0 ? 1f : 1f / (sm._velocity.y / 1000f);
                            var xOffset   = sm._velocity.x == 0 ? 0 : (Time.unscaledTime % xPeriod) / xPeriod * sm._scale;
                            var yOffset   = sm._velocity.y == 0 ? 0 : (Time.unscaledTime % yPeriod) / yPeriod * sm._scale;
                            mat.SetTexture(s_AlphaTex, sm._pattern);
                            mat.SetVector(s_AlphaTO, new Vector4(sm._scale * (Screen.width / (float)Screen.height) / aspectTex, sm._scale, xOffset, yOffset));
                        }

                        cmd.SetRenderTarget(data.buffer);
                        cmd.ClearRenderTarget(false, true, Color.clear);

                        foreach (var inst in _registered)
                        {
                            if (inst == null || inst._renderer == null)
                                continue;

                            if (inst.Color.a <= 0.01f)
                                continue;

                            cmd.SetGlobalColor(s_Color, inst.Color);

                            int submeshCount = 1;
                            if (inst._renderer is MeshRenderer mr && mr.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null)
                                submeshCount = mf.sharedMesh.subMeshCount;
                            else if (inst._renderer is SkinnedMeshRenderer smr && smr.sharedMesh != null)
                                submeshCount = smr.sharedMesh.subMeshCount;

                            cmd.SetGlobalTexture(s_MainTex, Texture2D.whiteTexture);

                            for (int sub = 0; sub < submeshCount; sub++)
                            {
                                var mats = inst._renderer.sharedMaterials;
                                if (sub < mats.Length && mats[sub]?.mainTexture != null)
                                    cmd.SetGlobalTexture(s_MainTex, mats[sub].mainTexture);

                                cmd.DrawRenderer(inst._renderer, mat, sub, 0);
                            }
                        }

                        cmd.SetGlobalVector(s_Step, data.owner._step);
                        cmd.SetGlobalTexture(s_MainTexId, data.buffer);
                        cmd.SetRenderTarget(data.cameraColor);
                        cmd.DrawMesh(k_ScreenMesh, Matrix4x4.identity, mat, 0, 1);
                    });
                }
            }

            public override void FrameCleanup(CommandBuffer cmd) { }
        }
    }
}
