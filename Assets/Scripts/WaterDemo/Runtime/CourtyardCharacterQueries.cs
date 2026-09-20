using UnityEngine;
using UnityEngine.SceneManagement;

namespace WaterCourtyard
{
    /// <summary>Allocation-free character queries, also valid in isolated physics scenes.</summary>
    public sealed class CourtyardCharacterQueries
    {
        readonly RaycastHit[] hits = new RaycastHit[24];
        readonly Collider[] overlaps = new Collider[24];

        public static float Radius(CharacterController body) => body.radius * Mathf.Max(Mathf.Abs(body.transform.lossyScale.x), Mathf.Abs(body.transform.lossyScale.z));
        public static float Height(CharacterController body) => Mathf.Max(Radius(body) * 2, body.height * Mathf.Abs(body.transform.lossyScale.y));
        public static Vector3 Feet(CharacterController body) => body.transform.TransformPoint(body.center) - Vector3.up * (Height(body) * .5f);
        static bool Own(CharacterController body, Collider other) => !other || other == body || other.transform.IsChildOf(body.transform);

        public bool FindGround(CharacterController body, out RaycastHit ground)
        {
            float radius = Radius(body) * .82f;
            int count = body.gameObject.scene.GetPhysicsScene().SphereCast(Feet(body) + Vector3.up * (radius + .12f), radius,
                Vector3.down, hits, .22f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            ground = default; float nearest = float.PositiveInfinity;
            float normalLimit = Mathf.Cos(body.slopeLimit * Mathf.Deg2Rad);
            for (int i = 0; i < count; i++)
                if (!Own(body, hits[i].collider) && hits[i].normal.y >= normalLimit && hits[i].distance < nearest)
                { ground = hits[i]; nearest = ground.distance; }
            return nearest < float.PositiveInfinity;
        }

        public bool Ray(CharacterController body, Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
        {
            int count = body.gameObject.scene.GetPhysicsScene().Raycast(origin, direction, hits, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            nearest = default; float best = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
                if (!Own(body, hits[i].collider) && hits[i].distance < best)
                { best = hits[i].distance; nearest = hits[i]; }
            return best < float.PositiveInfinity;
        }

        public bool CanOccupy(CharacterController body, Vector3 feet)
        {
            // A small inset tolerates contact with the landing surface, not penetrations into walls.
            float radius = Mathf.Max(.02f, Radius(body) - .025f);
            float height = Height(body);
            int count = body.gameObject.scene.GetPhysicsScene().OverlapCapsule(feet + Vector3.up * (radius + .04f),
                feet + Vector3.up * (height - radius), radius, overlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            for (int i = 0; i < count; i++) if (!Own(body, overlaps[i])) return false;
            return true;
        }
    }
}
