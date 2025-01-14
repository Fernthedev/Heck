﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CustomJSONData;
using CustomJSONData.CustomBeatmap;
using HarmonyLib;
using Heck.Animation;
using static Heck.HeckController;

namespace Heck.HarmonyPatches
{
    [HarmonyPatch(typeof(BeatmapDataTransformHelper))]
    [HarmonyPatch("CreateTransformedBeatmapData")]
    internal static class BeatmapDataTransformHelperCreateTransformedBeatmapData
    {
        // Tracks are created before anything else as it is possible for a wall/note to be removed, thus never making the track for it.
        [HarmonyPriority(Priority.High)]
        private static void Prefix(IReadonlyBeatmapData beatmapData, ref TrackBuilder __state)
        {
            if (beatmapData is not CustomBeatmapData customBeatmapData)
            {
                return;
            }

            TrackBuilder trackManager = new();
            foreach (IReadonlyBeatmapLineData readonlyBeatmapLineData in customBeatmapData.beatmapLinesData)
            {
                BeatmapLineData beatmapLineData = (BeatmapLineData)readonlyBeatmapLineData;
                foreach (BeatmapObjectData beatmapObjectData in beatmapLineData.beatmapObjectsData)
                {
                    Dictionary<string, object?> dynData;
                    switch (beatmapObjectData)
                    {
                        case CustomObstacleData obstacleData:
                            dynData = obstacleData.customData;
                            break;

                        case CustomNoteData noteData:
                            dynData = noteData.customData;
                            break;

                        default:
                            continue;
                    }

                    // for epic tracks thing
                    object? trackNameRaw = dynData.Get<object>(TRACK);
                    if (trackNameRaw == null)
                    {
                        continue;
                    }

                    IEnumerable<string> trackNames;
                    if (trackNameRaw is List<object> listTrack)
                    {
                        trackNames = listTrack.Cast<string>();
                    }
                    else
                    {
                        trackNames = new[] { (string)trackNameRaw };
                    }

                    foreach (string trackName in trackNames)
                    {
                        trackManager.AddTrack(trackName);
                    }
                }
            }

            __state = trackManager;
        }

        [HarmonyPriority(Priority.High)]
        private static void Postfix(IReadonlyBeatmapData __result, TrackBuilder __state)
        {
            if (__result is not CustomBeatmapData customBeatmapData)
            {
                return;
            }

            // Point definitions
            IDictionary<string, PointDefinition> pointDefinitions = new Dictionary<string, PointDefinition>();
            void AddPoint(string pointDataName, PointDefinition pointData)
            {
                if (!pointDefinitions.ContainsKey(pointDataName))
                {
                    pointDefinitions.Add(pointDataName, pointData);
                }
                else
                {
                    Log.Logger.Log($"Duplicate point defintion name, {pointDataName} could not be registered!", IPA.Logging.Logger.Level.Error);
                }
            }

            IEnumerable<Dictionary<string, object?>>? pointDefinitionsRaw = customBeatmapData.customData.Get<List<object>>(POINT_DEFINITIONS)?.Cast<Dictionary<string, object?>>();
            if (pointDefinitionsRaw != null)
            {
                foreach (Dictionary<string, object?> pointDefintionRaw in pointDefinitionsRaw)
                {
                    string pointName = pointDefintionRaw.Get<string>(NAME) ?? throw new InvalidOperationException("Failed to retrieve point name.");
                    PointDefinition pointData = PointDefinition.ListToPointDefinition(pointDefintionRaw.Get<List<object>>(POINTS)
                        ?? throw new InvalidOperationException("Failed to retrieve point array."));
                    AddPoint(pointName, pointData);
                }
            }

            customBeatmapData.customData["pointDefinitions"] = pointDefinitions;

            // Event definitions
            IDictionary<string, CustomEventData> eventDefinitions = new Dictionary<string, CustomEventData>();
            void AddEvent(string eventDefinitionName, CustomEventData eventDefinition)
            {
                if (!eventDefinitions.ContainsKey(eventDefinitionName))
                {
                    eventDefinitions.Add(eventDefinitionName, eventDefinition);
                }
                else
                {
                    Log.Logger.Log($"Duplicate event defintion name, {eventDefinitionName} could not be registered!", IPA.Logging.Logger.Level.Error);
                }
            }

            IEnumerable<Dictionary<string, object?>>? eventDefinitionsRaw = customBeatmapData.customData.Get<List<object>>(EVENT_DEFINITIONS)?.Cast<Dictionary<string, object?>>();
            if (eventDefinitionsRaw != null)
            {
                foreach (Dictionary<string, object?> eventDefinitionRaw in eventDefinitionsRaw)
                {
                    string eventName = eventDefinitionRaw.Get<string>(NAME) ?? throw new InvalidOperationException("Failed to retrieve event name.");
                    string type = eventDefinitionRaw.Get<string>("_type") ?? throw new InvalidOperationException("Failed to retrieve event type.");
                    Dictionary<string, object?> data = eventDefinitionRaw.Get<Dictionary<string, object?>>("_data")
                                                       ?? throw new InvalidOperationException("Failed to retrieve event data.");

                    AddEvent(eventName, new CustomEventData(-1, type, data));
                }
            }

            customBeatmapData.customData["eventDefinitions"] = eventDefinitions;

            StackTrace stackTrace = new();
            bool isMultiplayer = stackTrace.GetFrame(2).GetMethod().Name.Contains("MultiplayerConnectedPlayerInstaller");

            customBeatmapData.customData["isMultiplayer"] = isMultiplayer;
            customBeatmapData.customData["tracks"] = __state.Tracks;
            CustomDataDeserializer.InvokeDeserializeBeatmapData(isMultiplayer, customBeatmapData, __state);

            Log.Logger.Log(
                isMultiplayer ? "Deserializing multiplayer BeatmapData." : "Deserializing local player BeatmapData.",
                IPA.Logging.Logger.Level.Trace);
        }
    }
}
