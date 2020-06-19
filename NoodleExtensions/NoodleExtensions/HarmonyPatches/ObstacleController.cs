﻿using CustomJSONData;
using CustomJSONData.CustomBeatmap;
using HarmonyLib;
using IPA.Utilities;
using NoodleExtensions.Animation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using static NoodleExtensions.HarmonyPatches.SpawnDataHelper.BeatmapObjectSpawnMovementDataVariables;
using static NoodleExtensions.Plugin;

namespace NoodleExtensions.HarmonyPatches
{
    [NoodlePatch(typeof(ObstacleController))]
    [NoodlePatch("Init")]
    internal class ObstacleControllerInit
    {
        private static void Postfix(ObstacleController __instance, ObstacleData obstacleData, Vector3 startPos, Vector3 midPos, Vector3 endPos)
        {
            if (obstacleData is CustomObstacleData customData)
            {
                dynamic dynData = customData.customData;
                IEnumerable<float> localrot = ((List<object>)Trees.at(dynData, LOCALROTATION))?.Select(n => Convert.ToSingle(n));
                Quaternion localRotation = _quaternionIdentity;
                if (localrot != null)
                {
                    localRotation = Quaternion.Euler(localrot.ElementAt(0), localrot.ElementAt(1), localrot.ElementAt(2));
                    __instance.transform.rotation *= localRotation;
                }

                dynData.startPos = startPos;
                dynData.midPos = midPos;
                dynData.endPos = endPos;
                dynData.localRotation = localRotation;
            }
        }

        private static readonly MethodInfo _getCustomWidth = SymbolExtensions.GetMethodInfo(() => GetCustomWidth(0, null));
        private static readonly MethodInfo _getWorldRotation = SymbolExtensions.GetMethodInfo(() => GetWorldRotation(null, 0));
        private static readonly MethodInfo _getCustomLength = SymbolExtensions.GetMethodInfo(() => GetCustomLength(0, null));
        private static readonly MethodInfo _invertQuaternion = SymbolExtensions.GetMethodInfo(() => Quaternion.Inverse(Quaternion.identity));

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = instructions.ToList();
            bool foundRotation = false;
            bool foundWidth = false;
            bool foundLength = false;
            int instructrionListCount = instructionList.Count;
            for (int i = 0; i < instructrionListCount; i++)
            {
                if (!foundRotation &&
                       instructionList[i].opcode == OpCodes.Stfld &&
                       ((FieldInfo)instructionList[i].operand).Name == "_worldRotation")
                {
                    foundRotation = true;

                    instructionList[i - 1] = new CodeInstruction(OpCodes.Call, _getWorldRotation);
                    instructionList[i - 4] = new CodeInstruction(OpCodes.Ldarg_1);
                    instructionList.RemoveAt(i - 2);

                    instructionList.RemoveRange(i + 1, 2);
                    instructionList[i + 1] = new CodeInstruction(OpCodes.Ldarg_0);
                    instructionList[i + 2] = new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(ObstacleController), "_worldRotation"));
                    instructionList[i + 3] = new CodeInstruction(OpCodes.Call, _invertQuaternion);
                }
                if (!foundWidth &&
                    instructionList[i].opcode == OpCodes.Callvirt &&
                    ((MethodInfo)instructionList[i].operand).Name == "get_width")
                {
                    foundWidth = true;
                    instructionList.Insert(i + 2, new CodeInstruction(OpCodes.Ldarg_1));
                    instructionList.Insert(i + 3, new CodeInstruction(OpCodes.Call, _getCustomWidth));
                }
                if (!foundLength &&
                    instructionList[i].opcode == OpCodes.Stloc_2)
                {
                    foundLength = true;
                    instructionList.Insert(i, new CodeInstruction(OpCodes.Ldarg_1));
                    instructionList.Insert(i + 1, new CodeInstruction(OpCodes.Call, _getCustomLength));
                }
            }
            if (!foundRotation) Logger.Log("Failed to find _worldRotation stfld!", IPA.Logging.Logger.Level.Error);
            if (!foundWidth) Logger.Log("Failed to find get_width call!", IPA.Logging.Logger.Level.Error);
            if (!foundLength) Logger.Log("Failed to find stloc.2!", IPA.Logging.Logger.Level.Error);
            return instructionList.AsEnumerable();
        }

        private static Quaternion GetWorldRotation(ObstacleData obstacleData, float @default)
        {
            Quaternion worldRotation = Quaternion.Euler(0, @default, 0);
            if (obstacleData is CustomObstacleData customData)
            {
                dynamic dynData = customData.customData;
                dynamic rotation = Trees.at(dynData, ROTATION);

                if (rotation != null)
                {
                    if (rotation is List<object> list)
                    {
                        IEnumerable<float> _rot = list.Select(n => Convert.ToSingle(n));
                        worldRotation = Quaternion.Euler(_rot.ElementAt(0), _rot.ElementAt(1), _rot.ElementAt(2));
                    }
                    else
                    {
                        worldRotation = Quaternion.Euler(0, (float)rotation, 0);
                    }
                }
                dynData.worldRotation = worldRotation;
            }
            return worldRotation;
        }

        private static float GetCustomWidth(float @default, ObstacleData obstacleData)
        {
            if (obstacleData is CustomObstacleData customData)
            {
                dynamic dynData = customData.customData;
                IEnumerable<float?> scale = ((List<object>)Trees.at(dynData, SCALE))?.Select(n => n.ToNullableFloat());
                float? width = scale?.ElementAtOrDefault(0);
                if (width.HasValue) return width.Value;
            }
            return @default;
        }

        private static float GetCustomLength(float @default, ObstacleData obstacleData)
        {
            if (obstacleData is CustomObstacleData customData)
            {
                dynamic dynData = customData.customData;
                IEnumerable<float?> scale = ((List<object>)Trees.at(dynData, SCALE))?.Select(n => n.ToNullableFloat());
                float? length = scale?.ElementAtOrDefault(2);
                if (length.HasValue) return length.Value * _noteLinesDistance;
            }
            return @default;
        }
    }

    [NoodlePatch(typeof(ObstacleController))]
    [NoodlePatch("Update")]
    internal class ObstacleControllerUpdate
    {
        private static readonly FieldAccessor<ObstacleDissolve, CutoutAnimateEffect>.Accessor _obstacleCutoutAnimateEffectAccessor = FieldAccessor<ObstacleDissolve, CutoutAnimateEffect>.GetAccessor("_cutoutAnimateEffect");

        private static void Prefix(ObstacleController __instance, ObstacleData ____obstacleData, AudioTimeSyncController ____audioTimeSyncController, float ____startTimeOffset,
            ref Vector3 ____startPos, ref Vector3 ____midPos, ref Vector3 ____endPos, float ____move1Duration, float ____move2Duration, Quaternion ____worldRotation, Quaternion ____inverseWorldRotation)
        {
            if (____obstacleData is CustomObstacleData customData)
            {
                dynamic dynData = customData.customData;

                Track track = Trees.at(dynData, "track");
                dynamic animationObject = Trees.at(dynData, "_animation");
                if (track != null || animationObject != null)
                {
                    // idk i just copied base game time
                    float jumpDuration = ____move2Duration;
                    float elapsedTime = ____audioTimeSyncController.songTime - ____startTimeOffset;
                    float normalTime = (elapsedTime - ____move1Duration) / jumpDuration;

                    AnimationHelper.GetObjectOffset(animationObject, track, normalTime, out Vector3? positionOffset, out Quaternion? rotationOffset, out Vector3? scaleOffset, out Quaternion? localRotationOffset, out float? dissolve, out float? _);

                    if (positionOffset.HasValue)
                    {
                        Vector3 startPos = Trees.at(dynData, "startPos");
                        Vector3 midPos = Trees.at(dynData, "midPos");
                        Vector3 endPos = Trees.at(dynData, "endPos");

                        Vector3 offset = positionOffset.Value;
                        ____startPos = startPos + offset;
                        ____midPos = midPos + offset;
                        ____endPos = endPos + offset;
                    }

                    Transform transform = __instance.transform;

                    if (rotationOffset.HasValue || localRotationOffset.HasValue)
                    {
                        Quaternion worldRotation = Trees.at(dynData, "worldRotation");
                        Quaternion localRotation = Trees.at(dynData, "localRotation");

                        Quaternion worldRotationQuatnerion = worldRotation;
                        if (rotationOffset.HasValue)
                        {
                            worldRotationQuatnerion *= rotationOffset.Value;
                            Quaternion inverseWorldRotation = Quaternion.Inverse(worldRotationQuatnerion);
                            ____worldRotation = worldRotationQuatnerion;
                            ____inverseWorldRotation = inverseWorldRotation;
                        }

                        worldRotationQuatnerion *= localRotation;

                        if (localRotationOffset.HasValue) worldRotationQuatnerion *= localRotationOffset.Value;

                        transform.rotation = worldRotationQuatnerion;
                    }

                    if (scaleOffset.HasValue) transform.localScale = scaleOffset.Value;

                    if (dissolve.HasValue)
                    {
                        CutoutAnimateEffect cutoutAnimateEffect = Trees.at(dynData, "cutoutAnimateEffect");
                        if (cutoutAnimateEffect == null)
                        {
                            ObstacleDissolve obstacleDissolve = __instance.gameObject.GetComponent<ObstacleDissolve>();
                            cutoutAnimateEffect = _obstacleCutoutAnimateEffectAccessor(ref obstacleDissolve);
                            dynData.cutoutAnimateEffect = cutoutAnimateEffect;
                        }
                        cutoutAnimateEffect.SetCutout(dissolve.Value);
                    }
                }
            }
        }
    }

    [NoodlePatch(typeof(ObstacleController))]
    [NoodlePatch("GetPosForTime")]
    internal class ObstacleControllerGetPosForTime
    {
        private static bool Prefix(ref Vector3 __result, ObstacleData ____obstacleData, Vector3 ____startPos, Vector3 ____midPos,
            float ____move1Duration, float ____move2Duration, float time) {
            if (____obstacleData is CustomObstacleData customObstacleData)
            {
                dynamic dynData = customObstacleData.customData;
                dynamic animationObject = Trees.at(dynData, "_animation");
                Track track = AnimationHelper.GetTrack(dynData);
                AnimationHelper.GetDefinitePosition(animationObject, out PointData position);

                float jumpTime = Mathf.Clamp((time - ____move1Duration) / ____move2Duration, 0, 1);

                if (position != null || track?._pathDefinitePosition._basePointData != null)
                {
                    Vector3 noteOffset = Trees.at(dynData, "noteOffset");
                    Vector3 definitePosition = (position?.Interpolate(jumpTime) ?? track._pathDefinitePosition.Interpolate(jumpTime).Value) * _noteLinesDistance + noteOffset;
                    if (time < ____move1Duration)
                    {
                        __result = Vector3.LerpUnclamped(____startPos, ____midPos, time / ____move1Duration);
                        __result += definitePosition - ____midPos;
                    }
                    else
                    {
                        __result = definitePosition;
                    }
                    return false;
                }
            }
            return true;
        }
    }
}