// Standalone behavioral host for the real MotorLever source. Unity native physics is verified separately.
using System;
namespace UnityEngine
{
    public class Object { public static implicit operator bool(Object value) => value != null; }
    public class MonoBehaviour : Object { public bool isActiveAndEnabled = true; }
    public class DisallowMultipleComponent : Attribute { }
    public class RangeAttribute : Attribute { public RangeAttribute(float min, float max) { } }
    public class MinAttribute : Attribute { public MinAttribute(float min) { } }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string text) { } }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 right => new Vector3(1, 0, 0);
        public static Vector3 one => new Vector3(1, 1, 1);
    }
    public struct Quaternion
    {
        internal System.Numerics.Quaternion value;
        public static Quaternion identity => new Quaternion { value = System.Numerics.Quaternion.Identity };
        public static Quaternion AngleAxis(float angle, Vector3 axis) => new Quaternion { value = System.Numerics.Quaternion.CreateFromAxisAngle(new System.Numerics.Vector3(axis.x, axis.y, axis.z), angle * MathF.PI / 180) };
        public static Quaternion operator *(Quaternion a, Quaternion b) => new Quaternion { value = a.value * b.value };
    }
    public class Transform : Object
    {
        public Vector3 position;
        public Vector3 localScale = Vector3.one;
        public Quaternion localRotation = Quaternion.identity;
        public Vector3 InverseTransformPoint(Vector3 world)
        {
            var local = System.Numerics.Vector3.Transform(new System.Numerics.Vector3(world.x - position.x, world.y - position.y, world.z - position.z), System.Numerics.Quaternion.Inverse(localRotation.value));
            return new Vector3(local.X / localScale.x, local.Y / localScale.y, local.Z / localScale.z);
        }
    }
    public static class Mathf { public static float Abs(float value) => MathF.Abs(value); }
}
namespace CoastalTemple.Interaction { public class PlayerInteractor { } }
namespace CoastalTemple.Tutorial
{
    public abstract class TutorialInteractable : UnityEngine.MonoBehaviour
    {
        public string prompt = "Operate";
        public virtual bool Available => isActiveAndEnabled;
        public virtual string DisplayPrompt => prompt;
        public abstract void Use(CoastalTemple.Interaction.PlayerInteractor actor);
    }
}
namespace CoastalTemple.Mechanisms
{
    // Deliberately only the external motor contract. No cycle behavior is duplicated here.
    public class LinearPlatformMotor : UnityEngine.MonoBehaviour
    {
        public int Direction => isActiveAndEnabled ? ManualDirection : 0;
        public int ManualDirection { get; private set; }
        public void SetDirection(int direction) => ManualDirection = Math.Sign(direction);
    }
}
