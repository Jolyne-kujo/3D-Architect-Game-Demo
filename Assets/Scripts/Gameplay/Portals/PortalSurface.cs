using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WaterCourtyard;
using CoastalTemple.LightPuzzles;

namespace CoastalTemple.Portals
{
    [DisallowMultipleComponent, RequireComponent(typeof(LightPortal)), DefaultExecutionOrder(1000)]
    public sealed class PortalSurface : MonoBehaviour
    {
        [Tooltip("Thin cube child aligned with the local XY aperture. No collider; local rotation must be identity.")]
        public Renderer Screen;
        [Tooltip("Empty uses Camera.main. One observer per window; split screen and XR are not supported.")]
        public Camera Observer;
        public bool TransportTravellers = true;
        public bool RenderView = true;
        [Range(1, 4)] public int RecursionDepth = 2;
        [Range(.25f, 1)] public float ResolutionScale = .75f;
        [Range(128, 2048)] public int MaxTextureSize = 1024;
        [Min(.001f)] public float ClipPlaneOffset = .015f;
        public bool RenderShadows = true;
        [Tooltip("Assign Portal Clipped Lit so player body slicing is included in builds and exported prefab dependencies.")]
        public Shader TravellerClipShader;
        [Tooltip("Stretch a thin cube behind the plane enough to cover the observer near clipping plane.")]
        public bool ProtectNearPlane = true;
        public LightPortal Portal { get { if (!portal) portal = GetComponent<LightPortal>(); return portal; } }
        public RenderTexture CurrentTexture { get; private set; }
        public int LastRenderedFrame { get; private set; } = -1;
        public int RenderCount { get; private set; }
        internal static readonly List<PortalSurface> Active = new List<PortalSurface>();
        static int lastFrame = -1, recursiveBudget;
        const int PreferredRenderBudget=12;
        static bool rendering;
        static readonly List<PortalSurface> visibleRoots=new List<PortalSurface>();
        public static int LastRenderRequests { get; private set; }
        public int LastRootRenderedFrame { get; private set; } = -1;
        static float nextDiscovery;
        static readonly int TextureId = Shader.PropertyToID("_PortalTexture");
        static readonly int ValidId = Shader.PropertyToID("_PortalValid");
        readonly Camera[] cameras = new Camera[4];
        readonly RenderTexture[] textures = new RenderTexture[4];
        struct ViewState
        {
            public PortalSurface surface;
            public RenderTexture texture;
            public Vector3 position,scale;
            public bool hidden;
        }
        readonly ViewState[][] viewStates=new ViewState[4][];
        readonly UniversalRenderPipeline.SingleCameraRequest request = new UniversalRenderPipeline.SingleCameraRequest();
        LightPortal portal;
        MaterialPropertyBlock properties;
        Vector3 originalScreenPosition, originalScreenScale;
        bool screenPoseCaptured;

        void OnEnable()
        {
            if (!Active.Contains(this)) Active.Add(this);
            if (Screen) { originalScreenPosition = Screen.transform.localPosition; originalScreenScale = Screen.transform.localScale; screenPoseCaptured = true; }
            SetTexture(null);
            if (Application.isPlaying) DiscoverPlayers();
        }
        void OnDisable()
        {
            Active.Remove(this);
            if (Screen && screenPoseCaptured) { Screen.transform.localPosition = originalScreenPosition; Screen.transform.localScale = originalScreenScale; }
            SetTexture(null);
            for (int i = 0; i < 4; i++)
            {
                if (textures[i]) { textures[i].Release(); DestroyResource(textures[i]); textures[i] = null; }
                if (cameras[i]) { DestroyResource(cameras[i].gameObject); cameras[i] = null; }
            }
        }
        static void DestroyResource(Object value) { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
        static void DiscoverPlayers()
        {
            foreach (var walker in FindObjectsByType<CourtyardWalker>())
                if (!walker.GetComponent<PortalTraveller>()) walker.gameObject.AddComponent<PortalTraveller>();
            nextDiscovery = Time.unscaledTime + 1;
        }
        void LateUpdate()
        {
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            if (Time.unscaledTime >= nextDiscovery) DiscoverPlayers();
            RenderAllNow();
        }

        public static void RenderAllNow()
        {
            if(rendering)return;
            rendering=true;
            try
            {
                Active.RemoveAll(surface => !surface);
                visibleRoots.Clear();LastRenderRequests=0;
                foreach (var surface in Active)
                {
                    // Never expose a previous viewpoint as a live window when this view is skipped.
                    surface.SetTexture(null);
                    Camera observer = surface.Observer ? surface.Observer : Camera.main;
                    if(!observer)continue;
                    // Expand the screen before visibility testing: at the plane, a thin quad is
                    // entirely in front of the near clip plane and would otherwise never update.
                    surface.ProtectScreen(observer);
                    if(surface.CanSee(observer))visibleRoots.Add(surface);
                }
                // Reserve one fresh render for EVERY visible root. Only nested views share the
                // remaining budget; an early recursive tree cannot starve later visible windows.
                recursiveBudget=Mathf.Max(0,PreferredRenderBudget-visibleRoots.Count);
                foreach(var surface in visibleRoots)
                {
                    Camera observer=surface.Observer?surface.Observer:Camera.main;
                    if(observer)surface.RenderFrom(observer,observer.projectionMatrix,0,Mathf.Clamp(surface.RecursionDepth,1,4));
                }
            }
            finally
            {
                // Restore thickness for the real observer after virtual cameras have finished.
                foreach (var surface in Active)
                {
                    if (!surface) continue;
                    Camera observer = surface.Observer ? surface.Observer : Camera.main;
                    if (observer) surface.ProtectScreen(observer);
                }
                rendering=false;
            }
        }

        bool CanSee(Camera observer)
        {
            if (!isActiveAndEnabled || !RenderView || !Screen || !Screen.enabled || Screen.forceRenderingOff || !Portal.CanTransfer) return false;
            if ((observer.cullingMask & (1 << Screen.gameObject.layer)) == 0) return false;
            if (!Portal.TwoSided&&Vector3.Dot(observer.transform.position - transform.position, Portal.PlaneNormal) < -.005f) return false;
            return GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(observer), Screen.bounds);
        }

        void ProtectScreen(Camera observer)
        {
            if (!ProtectNearPlane || !Screen || Screen.transform.parent != transform) return;
            float halfHeight = observer.orthographic ? observer.orthographicSize : observer.nearClipPlane * Mathf.Tan(observer.fieldOfView * Mathf.Deg2Rad * .5f);
            float halfWidth = halfHeight * observer.aspect;
            float thickness = new Vector3(halfWidth, halfHeight, observer.nearClipPlane).magnitude + .025f;
            float localDepth = thickness / Mathf.Max(.0001f, transform.TransformVector(Vector3.forward).magnitude);
            var scale = Screen.transform.localScale; scale.z = Mathf.Max(.02f, localDepth);
            Screen.transform.localScale = scale;
            float side=Vector3.Dot(observer.transform.position-transform.position,Portal.PlaneNormal)<0?-1:1;
            var position = Screen.transform.localPosition; position.z = -side*scale.z * .5f;
            Screen.transform.localPosition = position;
        }

        void SetTexture(RenderTexture texture)
        {
            CurrentTexture = texture;
            if (!Screen) return;
            properties ??= new MaterialPropertyBlock();
            Screen.GetPropertyBlock(properties);
            properties.SetTexture(TextureId, texture ? texture : Texture2D.blackTexture);
            properties.SetFloat(ValidId, texture ? 1 : 0);
            Screen.SetPropertyBlock(properties);
        }

        void EnsureTarget(Camera observer, int depth)
        {
            if (!cameras[depth])
            {
                var go = new GameObject(name + " Portal Camera " + depth) { hideFlags = HideFlags.HideAndDontSave };
                cameras[depth] = go.AddComponent<Camera>(); cameras[depth].enabled = false;
                var data = go.AddComponent<UniversalAdditionalCameraData>();
                data.renderType = CameraRenderType.Base; data.renderPostProcessing = false;
                data.requiresColorOption = CameraOverrideOption.Off; data.requiresDepthOption = CameraOverrideOption.Off;
            }
            float reduction = Mathf.Pow(.8f, depth);
            float scale = Mathf.Min(ResolutionScale * reduction, MaxTextureSize / (float)Mathf.Max(observer.pixelWidth, observer.pixelHeight));
            int width = Mathf.Max(64, Mathf.RoundToInt(observer.pixelWidth * scale));
            int height = Mathf.Max(64, Mathf.RoundToInt(observer.pixelHeight * scale));
            if (textures[depth] && textures[depth].width == width && textures[depth].height == height)
            { if(!textures[depth].IsCreated())textures[depth].Create();return; }
            if (textures[depth]) { textures[depth].Release(); DestroyResource(textures[depth]); }
            textures[depth] = new RenderTexture(width, height, 24, RenderTextureFormat.DefaultHDR)
            { name = name + " Portal View " + depth, antiAliasing = 1, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            textures[depth].Create();
        }

        void RenderFrom(Camera observer, Matrix4x4 baseProjection, int depth, int limit)
        {
            if (depth >= 4 || (depth>0&&recursiveBudget<=0)) return;
            if(depth>0)recursiveBudget--;
            EnsureTarget(observer, depth);
            Camera camera = cameras[depth];
            camera.CopyFrom(observer); camera.enabled = false; camera.targetTexture = textures[depth];
            if(camera.clearFlags==CameraClearFlags.Depth||camera.clearFlags==CameraClearFlags.Nothing)camera.clearFlags=CameraClearFlags.SolidColor;
            camera.rect = new Rect(0, 0, 1, 1); camera.useOcclusionCulling = false; camera.allowMSAA = false;
            Matrix4x4 mapping = Portal.TransferMatrix;
            camera.transform.SetPositionAndRotation(mapping.MultiplyPoint3x4(observer.transform.position), PortalSpace.MapRotation(mapping, observer.transform.rotation));
            // Keep the exact affine view, including unequal positive portal scale, rather than losing it in Transform rotation.
            camera.worldToCameraMatrix = observer.worldToCameraMatrix * mapping.inverse;
            camera.projectionMatrix = baseProjection;
            var destination = Portal.Paired;
            float entrySide=Vector3.Dot(observer.transform.position-transform.position,Portal.PlaneNormal)<0?-1:1;
            Vector3 normal = destination.PlaneNormal*entrySide;
            float distance = Vector3.Dot(camera.transform.position - destination.transform.position, normal);
            if (distance < -.04f)
            {
                Vector3 point = destination.transform.position + normal * ClipPlaneOffset;
                Vector4 worldPlane = new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(normal, point));
                Vector4 cameraPlane = camera.worldToCameraMatrix.inverse.transpose * worldPlane;
                camera.projectionMatrix = camera.CalculateObliqueMatrix(cameraPlane);
            }
            camera.cullingMatrix = camera.projectionMatrix * camera.worldToCameraMatrix;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderShadows = RenderShadows; data.renderPostProcessing = false; data.renderType = CameraRenderType.Base;
            int count=Active.Count;
            if(viewStates[depth]==null||viewStates[depth].Length<count)viewStates[depth]=new ViewState[count];
            var saved=viewStates[depth];
            var exitSurface = destination.GetComponent<PortalSurface>();
            bool succeeded=false;
            try
            {
                for(int i=0;i<count;i++)
                {
                    var surface=Active[i];var screen=surface.Screen;
                    saved[i]=new ViewState{surface=surface,texture=surface.CurrentTexture,position=screen?screen.transform.localPosition:Vector3.zero,
                        scale=screen?screen.transform.localScale:Vector3.one,hidden=screen&&screen.forceRenderingOff};
                    surface.SetTexture(null);surface.ProtectScreen(camera);
                }
                if (exitSurface && exitSurface.Screen) exitSurface.Screen.forceRenderingOff = true;
                if (depth + 1 < limit)
                    foreach (var next in Active)
                        if (next != exitSurface && next.CanSee(camera)) next.RenderFrom(camera, baseProjection, depth + 1, limit);
                request.destination = textures[depth];
                if (RenderPipeline.SupportsRenderRequest(camera, request))
                {
                    RenderPipeline.SubmitRenderRequest(camera, request);succeeded=true;LastRenderRequests++;
                    LastRenderedFrame = Time.frameCount; RenderCount++;
                    if(depth==0)LastRootRenderedFrame=Time.frameCount;
                }
            }
            finally
            {
                // Each recursive branch owns a complete render context. Restoring only the
                // texture leaks screen thickness / hidden flags into its parent and siblings.
                for(int i=0;i<count;i++)
                {
                    var state=saved[i];if(!state.surface)continue;state.surface.SetTexture(state.texture);
                    if(state.surface.Screen){state.surface.Screen.transform.localPosition=state.position;state.surface.Screen.transform.localScale=state.scale;state.surface.Screen.forceRenderingOff=state.hidden;}
                }
            }
            SetTexture(succeeded?textures[depth]:null);
        }
    }
}
