using CoastalTemple.Player;
using UnityEngine;
using WaterCourtyard;

namespace CoastalTemple.Editor
{
    // Editor-only authoring: the serialized prefab contains every visible body part and joint.
    public static class PlayerAvatarAuthoring
    {
        public static string RunStackedWaterChecks()
        {
            var report = new System.Collections.Generic.List<string>();
            GameObject root = null;
            var meshes = new System.Collections.Generic.List<Mesh>();
            void Check(bool good, string name)
            {
                report.Add((good ? "PASS " : "FAIL ") + name);
                if (!good) throw new System.InvalidOperationException(string.Join("\n", report));
            }
            Courtyard.Water.WaterVolume Volume(string name, float originY, float bottom)
            {
                var go = new GameObject(name); go.transform.SetParent(root.transform, false);
                go.transform.position = new Vector3(9000, originY, 9000);
                var volume = go.AddComponent<Courtyard.Water.WaterVolume>();
                volume.sizeX = 6; volume.sizeZ = 8; volume.cellSize = 1;
                volume.bottom = bottom; volume.initialLevel = 0; volume.outletArea = 0;
                volume.Initialize(); meshes.Add(go.GetComponent<MeshFilter>().sharedMesh);
                return volume;
            }
            try
            {
                root = new GameObject("TemporaryStackedWaterChecks"); root.SetActive(false);
                var sea = Volume("LowerSea", 0, -4);
                var pool = Volume("RaisedPool", 3, -3);
                var actor = new GameObject("Walker"); actor.transform.SetParent(root.transform, false);
                actor.transform.position = new Vector3(9000, 1, 9000);
                var walker = actor.AddComponent<CourtyardWalker>();
                walker.additionalWaters = new[] { sea, pool };
                root.SetActive(true);
                Vector3 inside = new Vector3(9000, 1.6f, 9000);
                var chosen = walker.SampleWater(inside, out float surface, out _, out float depth);
                Check(chosen == pool && Mathf.Abs(surface - 3) < .001f && Mathf.Abs(depth - 3) < .001f,
                    "raised pool wins over earlier overlapping lower sea with its own surface/depth");
                Check(CourtyardSwimMotion.ShouldSwim(chosen, .8f, surface, depth, false),
                    "raised-pool immersion enters swimming instead of following lower sea depth");
                chosen = walker.SampleWater(new Vector3(9000, -.2f, 9000), out surface, out _, out _);
                Check(chosen == sea && Mathf.Abs(surface) < .001f,
                    "below raised basin bottom selects lower sea rather than water through the floor");
                Check(walker.SampleWater(new Vector3(9000, -.04f, 9000), out _, out _, out _) == pool,
                    "five-centimetre bed tolerance accepts small contact uncertainty");
                Check(walker.SampleWater(new Vector3(9000, -.06f, 9000), out _, out _, out _) == sea,
                    "below the bed tolerance excludes raised water");
                chosen = walker.SampleWater(new Vector3(9000, 4.8f, 9000), out surface, out _, out depth);
                Check(chosen == pool && !CourtyardSwimMotion.ShouldSwim(chosen, 4, surface, depth, false),
                    "airborne above highest pool stays airborne and cannot snap down into swimming");
                walker.additionalWaters = new[] { pool, sea };
                Check(walker.SampleWater(inside, out _, out _, out _) == pool,
                    "overlap selection is independent of additional-water ordering");
                walker.water = sea; walker.additionalWaters = new[] { pool };
                Check(walker.SampleWater(inside, out _, out _, out _) == pool,
                    "higher eligible pool also overrides a lower primary water");
                sea.initialLevel = 3; sea.ResetWater();
                Check(walker.SampleWater(inside, out _, out _, out _) == sea,
                    "equal-height surfaces preserve the explicitly assigned primary water");
                chosen = walker.SampleWater(new Vector3(9000, -4.2f, 9000), out surface, out Vector3 flow, out depth);
                Check(!chosen && surface == 0 && depth == 0 && flow == Vector3.zero,
                    "below every physical bed returns no water and clears sample outputs");
                return string.Join("\n", report) + "\n10 stacked-water component checks passed.";
            }
            finally
            {
                // Remove temporary generated meshes explicitly so this helper also cleans up in edit mode.
                foreach (var mesh in meshes) if (mesh) UnityEngine.Object.DestroyImmediate(mesh);
                if (root) UnityEngine.Object.DestroyImmediate(root);
            }
        }

        public static string RunComponentChecks()
        {
            var checks = new System.Collections.Generic.List<string>();
            GameObject player = null, wall = null, trigger = null;
            void Check(bool condition, string name)
            {
                checks.Add((condition ? "PASS " : "FAIL ") + name);
                if (!condition) throw new System.InvalidOperationException(string.Join("\n", checks));
            }
            try
            {
                player = new GameObject("TemporaryPlayerComponentCheck");
                player.transform.position = new Vector3(10000, 10000, 10000);
                var walker = player.AddComponent<CourtyardWalker>();
                var avatar = BuildAvatar(player.transform, null, null, null);
                Check(avatar.poseRoot && avatar.leftShoulder && avatar.rightShoulder && avatar.leftElbow && avatar.rightElbow &&
                    avatar.leftHip && avatar.rightHip && avatar.leftKnee && avatar.rightKnee, "all nine articulated pose transforms are authored");
                Check(avatar.bodyRenderers.Length == 26, "26 visible humanoid body parts are authored");
                Check(avatar.GetComponentsInChildren<Collider>().Length == 0, "visible avatar cannot block its controller or follow camera");
                avatar.SetVisible(false);
                Check(System.Array.TrueForAll(avatar.bodyRenderers, r => !r.enabled), "explicit first person hides every body part");
                avatar.SetVisible(true);
                Check(System.Array.TrueForAll(avatar.bodyRenderers, r => r.enabled), "third person restores every body part");
                var rig = player.AddComponent<CoastalPlayerCamera>(); rig.walker = walker; rig.avatar = avatar;
                rig.SetThirdPerson(false); rig.SetOverview(true);
                Check(System.Array.TrueForAll(avatar.bodyRenderers, r => r.enabled), "overview shows the avatar even after first person");
                Vector3 pivot = player.transform.position + Vector3.up * 1.48f;
                var ownCollider = new GameObject("TemporaryOwnedCollider");
                ownCollider.transform.SetParent(player.transform, false);
                ownCollider.transform.position = pivot + Vector3.back * .5f;
                ownCollider.AddComponent<BoxCollider>().size = Vector3.one * .2f;
                wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "TemporaryCameraObstacle"; wall.transform.position = pivot + Vector3.back * 2;
                wall.transform.localScale = new Vector3(3, 3, .4f);
                trigger = GameObject.CreatePrimitive(PrimitiveType.Cube);
                trigger.name = "TemporaryCameraTrigger"; trigger.transform.position = pivot + Vector3.back * .8f;
                trigger.transform.localScale = Vector3.one * .2f; trigger.GetComponent<Collider>().isTrigger = true;
                Physics.SyncTransforms();
                float hit = rig.NearestObstruction(pivot, Vector3.back, 4.2f);
                Check(hit > 1.5f && hit < 1.75f, "follow spherecast hits real wall while ignoring trigger");
                UnityEngine.Object.DestroyImmediate(wall); wall = null;
                Physics.SyncTransforms();
                Check(float.IsPositiveInfinity(rig.NearestObstruction(pivot, Vector3.back, 4.2f)), "camera ignores player and trigger after wall is removed");
                return string.Join("\n", checks) + "\n8 Unity component checks passed.";
            }
            finally
            {
                if (trigger) UnityEngine.Object.DestroyImmediate(trigger);
                if (wall) UnityEngine.Object.DestroyImmediate(wall);
                if (player) UnityEngine.Object.DestroyImmediate(player);
            }
        }

        public static CoastalPlayerAvatar BuildAvatar(Transform player, Material white, Material yellow, Material dark)
        {
            var root = new GameObject("PrototypeExplorer");
            root.transform.SetParent(player, false);
            var avatar = root.AddComponent<CoastalPlayerAvatar>();
            avatar.walker = player.GetComponent<CourtyardWalker>();
            avatar.poseRoot = Joint("BodyPose", root.transform, new Vector3(0, .95f, 0));
            var pose = avatar.poseRoot;
            Part("Torso", PrimitiveType.Cube, pose, new Vector3(0, .27f, 0), new Vector3(.43f, .54f, .27f), white);
            Part("YellowVest", PrimitiveType.Cube, pose, new Vector3(0, .41f, 0), new Vector3(.455f, .23f, .286f), yellow);
            Part("Belt", PrimitiveType.Cube, pose, new Vector3(0, .035f, 0), new Vector3(.44f, .09f, .285f), dark);
            Part("Neck", PrimitiveType.Cylinder, pose, new Vector3(0, .585f, 0), new Vector3(.12f, .07f, .12f), dark);
            Part("Head", PrimitiveType.Sphere, pose, new Vector3(0, .75f, 0), new Vector3(.32f, .35f, .31f), white);
            Part("Helmet", PrimitiveType.Sphere, pose, new Vector3(0, .845f, -.01f), new Vector3(.365f, .20f, .355f), yellow);
            Part("FaceVisor", PrimitiveType.Cube, pose, new Vector3(0, .76f, .143f), new Vector3(.255f, .105f, .056f), dark);
            Part("Backpack", PrimitiveType.Cube, pose, new Vector3(0, .28f, -.19f), new Vector3(.29f, .35f, .13f), dark);
            avatar.leftShoulder = Arm(pose, -1, white, yellow, dark, out avatar.leftElbow);
            avatar.rightShoulder = Arm(pose, 1, white, yellow, dark, out avatar.rightElbow);
            avatar.leftHip = Leg(pose, -1, white, dark, out avatar.leftKnee);
            avatar.rightHip = Leg(pose, 1, white, dark, out avatar.rightKnee);
            avatar.bodyRenderers = root.GetComponentsInChildren<Renderer>();
            foreach (Transform part in root.GetComponentsInChildren<Transform>()) part.gameObject.layer = player.gameObject.layer;
            return avatar;
        }

        static Transform Arm(Transform pose, float side, Material white, Material yellow, Material dark, out Transform elbow)
        {
            string name = side < 0 ? "Left" : "Right";
            var shoulder = Joint(name + "Shoulder", pose, new Vector3(side * .285f, .465f, 0));
            Part(name + "ShoulderShell", PrimitiveType.Sphere, shoulder, Vector3.zero, Vector3.one * .185f, yellow);
            Part(name + "UpperArm", PrimitiveType.Capsule, shoulder, new Vector3(0, -.155f, 0), new Vector3(.115f, .16f, .115f), white);
            elbow = Joint(name + "Elbow", shoulder, new Vector3(0, -.32f, 0));
            Part(name + "ElbowJoint", PrimitiveType.Sphere, elbow, Vector3.zero, Vector3.one * .115f, dark);
            Part(name + "Forearm", PrimitiveType.Capsule, elbow, new Vector3(0, -.135f, 0), new Vector3(.11f, .14f, .11f), white);
            Part(name + "Hand", PrimitiveType.Sphere, elbow, new Vector3(0, -.29f, .015f), new Vector3(.125f, .155f, .115f), dark);
            return shoulder;
        }

        static Transform Leg(Transform pose, float side, Material white, Material dark, out Transform knee)
        {
            string name = side < 0 ? "Left" : "Right";
            var hip = Joint(name + "Hip", pose, new Vector3(side * .125f, -.015f, 0));
            Part(name + "Thigh", PrimitiveType.Capsule, hip, new Vector3(0, -.205f, 0), new Vector3(.16f, .22f, .17f), white);
            knee = Joint(name + "Knee", hip, new Vector3(0, -.425f, 0));
            Part(name + "KneeJoint", PrimitiveType.Sphere, knee, Vector3.zero, Vector3.one * .155f, dark);
            Part(name + "Shin", PrimitiveType.Capsule, knee, new Vector3(0, -.185f, 0), new Vector3(.14f, .20f, .15f), white);
            Part(name + "Boot", PrimitiveType.Cube, knee, new Vector3(0, -.405f, .055f), new Vector3(.18f, .13f, .30f), dark);
            return hip;
        }

        static Transform Joint(string name, Transform parent, Vector3 position)
        {
            var joint = new GameObject(name).transform;
            joint.SetParent(parent, false); joint.localPosition = position; return joint;
        }

        static void Part(string name, PrimitiveType shape, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(shape);
            part.name = name; part.transform.SetParent(parent, false); part.transform.localPosition = position; part.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
