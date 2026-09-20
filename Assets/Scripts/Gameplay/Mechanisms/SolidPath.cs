using UnityEngine;

namespace CoastalTemple.Mechanisms
{
    /// <summary>Switches only explicitly assigned, pre-authored visible geometry and collision.</summary>
    [DisallowMultipleComponent]
    public sealed class SolidPath : MonoBehaviour
    {
        public Renderer[] renderers;
        public Collider[] colliders;
        public bool initiallySolid;
        public bool IsSolid { get; private set; }
        bool initialized;

        void Awake() => SetSolid(initiallySolid);
        void OnEnable() => SetSolid(initiallySolid);
        void OnDisable() => SetSolid(false);

        public void SetSolid(bool solid)
        {
            solid &= isActiveAndEnabled;
            if (initialized && IsSolid == solid) return;
            initialized = true;
            IsSolid = solid;
            if (renderers != null)
                for (int i = 0; i < renderers.Length; i++)
                    if (renderers[i]) renderers[i].enabled = solid;
            if (colliders != null)
                for (int i = 0; i < colliders.Length; i++)
                    if (colliders[i]) colliders[i].enabled = solid;
        }
    }
}
