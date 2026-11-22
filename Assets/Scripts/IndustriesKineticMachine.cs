using System;
using UnityEngine;

/// <summary>Mouvements décoratifs des petites machines d’Industries (GDD, FX).</summary>
[DisallowMultipleComponent]
public sealed class IndustriesKineticMachine : MonoBehaviour
{
    public enum Motion { Turn, Rock, Slide }

    [Serializable]
    public struct Joint
    {
        public Transform pivot;
        public Motion motion;
        public Vector3 axis;
        public float cyclesPerSecond;
        public float travel;
        public float phase;
    }

    [SerializeField] private Joint[] joints = Array.Empty<Joint>();
    private Pose[] rest;

    private struct Pose
    {
        public Quaternion rotation;
        public Vector3 position;
        public Vector3 axis;
    }

    public void Configure(Joint[] definitions) => joints = definitions;

    private void Awake()
    {
        rest = new Pose[joints.Length];
        for (int i = 0; i < joints.Length; i++)
        {
            var joint = joints[i];
            if (joint.pivot == null || joint.pivot.parent == null) continue;
            var anchor = joint.pivot.parent;
            Vector3 axis = joint.motion == Motion.Slide
                ? anchor.InverseTransformVector(transform.TransformVector(joint.axis.normalized))
                : anchor.InverseTransformDirection(transform.TransformDirection(joint.axis.normalized));
            rest[i] = new Pose { position = joint.pivot.localPosition, rotation = joint.pivot.localRotation, axis = axis };
        }
    }

    private void LateUpdate()
    {
        if (Time.deltaTime <= 0f) return;
        float time = Time.time;
        for (int i = 0; i < joints.Length; i++)
        {
            var joint = joints[i];
            if (joint.pivot == null || rest[i].axis == Vector3.zero) continue;
            var pose = rest[i];
            float turn = Mathf.Repeat(time * joint.cyclesPerSecond + joint.phase, 1f);
            float wave = Mathf.Sin(turn * 2f * Mathf.PI);
            if (joint.motion == Motion.Slide)
                joint.pivot.localPosition = pose.position + pose.axis * (wave * joint.travel);
            else
            {
                float angle = joint.motion == Motion.Turn ? turn * 360f : wave * joint.travel;
                joint.pivot.localRotation = Quaternion.AngleAxis(angle, pose.axis) * pose.rotation;
            }
        }
    }
}
