﻿using Chroma.Lighting.EnvironmentEnhancement;
using Heck;
using JetBrains.Annotations;
using UnityEngine;

namespace Chroma.HarmonyPatches.EnvironmentComponent
{
    // This whole file effectively changes _rotZ and _posZ from directly affecting the coordinate to being an offset
    [HeckPatch(typeof(TrackLaneRing))]
    [HeckPatch("Init")]
    internal static class TrackLaneRingInit
    {
        [UsedImplicitly]
        private static void Postfix(ref float ____posZ, Vector3 position)
        {
            ____posZ = position.z;
        }
    }

    [HeckPatch(typeof(TrackLaneRing))]
    [HeckPatch("FixedUpdateRing")]
    internal static class TrackLaneRingFixedUpdateRing
    {
        [UsedImplicitly]
        private static bool Prefix(
            float fixedDeltaTime,
            ref float ____prevRotZ,
            ref float ____rotZ,
            ref float ____prevPosZ,
            ref float ____posZ,
            float ____destRotZ,
            float ____rotationSpeed,
            float ____destPosZ,
            float ____moveSpeed)
        {
            ____prevRotZ = ____rotZ;
            ____rotZ = Mathf.Lerp(____rotZ, ____destRotZ, fixedDeltaTime * ____rotationSpeed);
            ____prevPosZ = ____posZ;
            ____posZ = Mathf.Lerp(____posZ, ____destPosZ, fixedDeltaTime * ____moveSpeed);

            return false;
        }
    }

    [HeckPatch(typeof(TrackLaneRing))]
    [HeckPatch("LateUpdateRing")]
    internal static class TrackLaneRingLateUpdateRing
    {
        [UsedImplicitly]
        private static bool Prefix(
            TrackLaneRing __instance,
            float interpolationFactor,
            float ____prevRotZ,
            float ____rotZ,
            Vector3 ____positionOffset,
            float ____prevPosZ,
            float ____posZ,
            Transform ____transform)
        {
            if (!EnvironmentEnhancementManager.RingRotationOffsets.TryGetValue(__instance, out Quaternion rotation))
            {
                rotation = Quaternion.identity;
            }

            float interpolatedZPos = ____prevPosZ + ((____posZ - ____prevPosZ) * interpolationFactor);
            Vector3 positionZOffset = rotation * Vector3.forward * interpolatedZPos;
            Vector3 pos = ____positionOffset + positionZOffset;

            float interpolatedZRot = ____prevRotZ + ((____rotZ - ____prevRotZ) * interpolationFactor);
            Quaternion rotationZOffset = Quaternion.AngleAxis(interpolatedZRot, Vector3.forward);
            Quaternion rot = rotation * rotationZOffset;

            ____transform.localRotation = rot;
            ____transform.localPosition = pos;

            return false;
        }
    }
}
