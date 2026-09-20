using System.Collections.Generic;
using CoastalTemple.LightPuzzles;
using CoastalTemple.Player;
using UnityEngine;

namespace CoastalTemple.Portals
{
    /// <summary>Near a portal, two clipped renderer-only views share one animated pose.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(PortalTraveller)), DefaultExecutionOrder(250)]
    public sealed class PortalTravellerVisual : MonoBehaviour
    {
        [Tooltip("Optional explicit body renderers. Empty uses the avatar body, then ordinary mesh renderers.")]
        public Renderer[] SourceRenderers;
        [Min(0)] public float ActivationPadding = .12f;
        public bool VisualsEnabled = true;
        public bool IsSlicing => ActivePortal;
        public LightPortal ActivePortal { get; private set; }
        public int GhostRendererCount => bindings.Count;
        public Transform GhostRoot => ghostRoot ? ghostRoot.transform : null;
        static readonly int ClipPlaneId = Shader.PropertyToID("_PortalClipPlane");
        static readonly int ClipEnabledId = Shader.PropertyToID("_PortalClipEnabled");
        sealed class Binding
        {
            public Renderer source;
            public MeshRenderer ghost;
            public Material[] originals, clipped;
            public MaterialPropertyBlock originalBlock, sourceBlock, ghostBlock;
            public Mesh mesh;
            public SkinnedMeshRenderer skin;
            public Mesh sourceMesh;
            public bool ownsMesh;
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<Vector3> normals = new List<Vector3>();
            public readonly List<Vector4> tangents = new List<Vector4>();
        }
        readonly List<Binding> bindings = new List<Binding>();
        PortalTraveller traveller;
        GameObject ghostRoot;
        Renderer[] sources;

        void Awake() => traveller = GetComponent<PortalTraveller>();
        void LateUpdate() => EvaluateNow();
        void OnDisable() => ClearVisuals();
        void OnDestroy() => ClearVisuals();

        public void EvaluateNow()
        {
            if (!traveller) traveller = GetComponent<PortalTraveller>();
            if (!VisualsEnabled || !traveller || !traveller.TransferEnabled) { ClearVisuals(); return; }
            if (sources == null)
            {
                var avatar = GetComponentInChildren<CoastalPlayerAvatar>(true);
                sources = SourceRenderers != null && SourceRenderers.Length > 0 ? SourceRenderers :
                    avatar && avatar.bodyRenderers != null && avatar.bodyRenderers.Length > 0 ? avatar.bodyRenderers : GetComponentsInChildren<Renderer>(true);
            }
            bool hasBounds = false;
            Bounds bounds = default;
            foreach (var renderer in sources)
            {
                if (!IsSupportedRenderer(renderer)) continue;
                if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; } else bounds.Encapsulate(renderer.bounds);
            }
            LightPortal selected = null;
            float nearest = float.PositiveInfinity;
            if (hasBounds)
                foreach (var surface in PortalSurface.Active)
                {
                    if (!surface || !surface.isActiveAndEnabled || !surface.TransportTravellers || !surface.Portal.CanTransfer) continue;
                    var portal = surface.Portal;
                    Vector3 localReference = portal.transform.InverseTransformPoint(traveller.CrossingPoint);
                    if (!portal.TwoSided&&localReference.z < -.02f) continue;
                    if (!OverlapsAperture(portal, bounds, ActivationPadding)) continue;
                    float distance = Mathf.Abs(Vector3.Dot(bounds.center - portal.transform.position, portal.PlaneNormal));
                    if (distance < nearest) { nearest = distance; selected = portal; }
                }
            if (!selected) { ClearVisuals(); return; }
            if (bindings.Count == 0 && !CreateVisuals(selected)) return;
            ActivePortal = selected;
            UpdateVisuals(selected);
        }

        static bool IsSupportedRenderer(Renderer renderer) => renderer && renderer.gameObject.activeInHierarchy &&
            (renderer is SkinnedMeshRenderer || renderer is MeshRenderer) && renderer.GetComponent<PortalSurface>() == null;

        public static bool OverlapsAperture(LightPortal portal, Bounds bounds, float padding)
        {
            Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 local = portal.transform.InverseTransformPoint(corner);
                min = Vector3.Min(min, local); max = Vector3.Max(max, local);
            }
            float localPadding = padding / Mathf.Max(.0001f, portal.transform.TransformVector(Vector3.forward).magnitude);
            return min.z <= localPadding && max.z >= -localPadding &&
                min.x < portal.Aperture.x * .5f && max.x > -portal.Aperture.x * .5f &&
                min.y < portal.Aperture.y * .5f && max.y > -portal.Aperture.y * .5f;
        }

        bool CreateVisuals(LightPortal portal)
        {
            var surface = portal.GetComponent<PortalSurface>();
            Shader shader = surface && surface.TravellerClipShader ? surface.TravellerClipShader : Shader.Find("Coastal Temple/Portal Clipped Lit");
            if (!shader || !shader.isSupported) return false;
            ghostRoot = new GameObject(name + " Portal Body Image") { hideFlags = HideFlags.DontSave };
            foreach (var source in sources)
            {
                if (!IsSupportedRenderer(source)) continue;
                var skin = source as SkinnedMeshRenderer;
                var filter = source.GetComponent<MeshFilter>();
                var sourceMesh = skin ? skin.sharedMesh : filter ? filter.sharedMesh : null;
                if (!sourceMesh) continue;
                var originals = source.sharedMaterials;
                bool supported = originals.Length > 0;
                foreach (var material in originals)
                    supported &= material && material.HasProperty("_BaseColor") && material.HasProperty("_BaseMap") &&
                        (!material.HasProperty("_Surface") || material.GetFloat("_Surface") < .5f);
                if (!supported) continue;
                var go = new GameObject(source.name + " Sliced Image") { hideFlags = HideFlags.DontSave, layer = source.gameObject.layer };
                go.transform.SetParent(ghostRoot.transform, false);
                var ghost = go.AddComponent<MeshRenderer>();
                var meshFilter = go.AddComponent<MeshFilter>();
                var binding = new Binding
                {
                    source = source, ghost = ghost, originals = originals, skin = skin, sourceMesh = sourceMesh,
                    clipped = new Material[originals.Length], originalBlock = new MaterialPropertyBlock(),
                    sourceBlock = new MaterialPropertyBlock(), ghostBlock = new MaterialPropertyBlock()
                };
                source.GetPropertyBlock(binding.originalBlock);
                for (int i = 0; i < originals.Length; i++)
                {
                    binding.clipped[i] = new Material(originals[i]) { shader = shader, name = originals[i].name + " Portal Slice", hideFlags = HideFlags.DontSave };
                    binding.clipped[i].SetFloat(ClipEnabledId, 1);
                }
                binding.ownsMesh = skin || sourceMesh.isReadable;
                binding.mesh = skin ? new Mesh() : binding.ownsMesh ? Instantiate(sourceMesh) : sourceMesh;
                if (binding.ownsMesh) { binding.mesh.name = source.name + " Portal Pose"; binding.mesh.hideFlags = HideFlags.DontSave; binding.mesh.MarkDynamic(); }
                meshFilter.sharedMesh = binding.mesh;
                source.sharedMaterials = binding.clipped; ghost.sharedMaterials = binding.clipped;
                ghost.receiveShadows = source.receiveShadows; ghost.lightProbeUsage = source.lightProbeUsage;
                ghost.reflectionProbeUsage = source.reflectionProbeUsage; ghost.renderingLayerMask = source.renderingLayerMask;
                bindings.Add(binding);
            }
            if (bindings.Count > 0) return true;
            ClearVisuals(); return false;
        }

        void UpdateVisuals(LightPortal portal)
        {
            Matrix4x4 mapping = portal.TransferMatrix;
            float side=Vector3.Dot(traveller.CrossingPoint-portal.transform.position,portal.PlaneNormal)<0?-1:1;
            Vector4 sourcePlane = ClipPlane(portal)*side, destinationPlane = ClipPlane(portal.Paired)*side;
            foreach (var binding in bindings)
            {
                if (!binding.source || !binding.ghost) continue;
                binding.ghost.enabled = binding.source.enabled && binding.source.gameObject.activeInHierarchy;
                binding.ghost.shadowCastingMode = binding.source.shadowCastingMode;
                binding.source.GetPropertyBlock(binding.sourceBlock);
                binding.sourceBlock.SetVector(ClipPlaneId, sourcePlane); binding.sourceBlock.SetFloat(ClipEnabledId, 1);
                binding.source.SetPropertyBlock(binding.sourceBlock);
                binding.source.GetPropertyBlock(binding.ghostBlock);
                binding.ghostBlock.SetVector(ClipPlaneId, destinationPlane); binding.ghostBlock.SetFloat(ClipEnabledId, 1);
                binding.ghost.SetPropertyBlock(binding.ghostBlock);
                Matrix4x4 pose = mapping * binding.source.localToWorldMatrix;
                Vector3 origin = pose.MultiplyPoint3x4(Vector3.zero);
                if (binding.ownsMesh)
                {
                    if (binding.skin) binding.skin.BakeMesh(binding.mesh, false);
                    var sourceMesh = binding.skin ? binding.mesh : binding.sourceMesh;
                    sourceMesh.GetVertices(binding.vertices); sourceMesh.GetNormals(binding.normals); sourceMesh.GetTangents(binding.tangents);
                    Matrix4x4 normalMatrix = pose.inverse.transpose;
                    for (int i = 0; i < binding.vertices.Count; i++) binding.vertices[i] = pose.MultiplyVector(binding.vertices[i]);
                    for (int i = 0; i < binding.normals.Count; i++) binding.normals[i] = normalMatrix.MultiplyVector(binding.normals[i]).normalized;
                    for (int i = 0; i < binding.tangents.Count; i++)
                    {
                        Vector4 tangent = binding.tangents[i]; Vector3 direction = pose.MultiplyVector(tangent).normalized;
                        binding.tangents[i] = new Vector4(direction.x, direction.y, direction.z, tangent.w);
                    }
                    binding.mesh.SetVertices(binding.vertices); binding.mesh.SetNormals(binding.normals);
                    if (binding.tangents.Count > 0) binding.mesh.SetTangents(binding.tangents);
                    binding.mesh.RecalculateBounds();
                    binding.ghost.transform.SetPositionAndRotation(origin, Quaternion.identity); binding.ghost.transform.localScale = Vector3.one;
                }
                else
                {
                    // Unreadable static meshes still support ordinary TRS portals without enabling Read/Write.
                    binding.ghost.transform.SetPositionAndRotation(origin, PortalSpace.MapRotation(mapping, binding.source.transform.rotation));
                    binding.ghost.transform.localScale = new Vector3(pose.GetColumn(0).magnitude, pose.GetColumn(1).magnitude, pose.GetColumn(2).magnitude);
                }
            }
        }

        public static Vector4 ClipPlane(LightPortal portal)
        {
            Vector3 normal = portal.PlaneNormal;
            return new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(normal, portal.transform.position));
        }

        public void ClearVisuals()
        {
            ActivePortal = null;
            foreach (var binding in bindings)
            {
                if (binding.source) { binding.source.sharedMaterials = binding.originals; binding.source.SetPropertyBlock(binding.originalBlock); }
                foreach (var material in binding.clipped) if (material) Dispose(material);
                if (binding.ownsMesh && binding.mesh) Dispose(binding.mesh);
            }
            bindings.Clear();
            if (ghostRoot) { ghostRoot.SetActive(false); Dispose(ghostRoot); ghostRoot = null; }
        }
        static void Dispose(Object value) { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
    }
}
