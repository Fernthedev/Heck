﻿namespace Chroma.HarmonyPatches
{
    using Chroma.Events;
    using CustomJSONData;
    using CustomJSONData.CustomBeatmap;
    using HarmonyLib;
    using UnityEngine;

    [HarmonyPatch(typeof(NoteCutEffectSpawner))]
    [HarmonyPatch("SpawnNoteCutEffect")]
    internal class NoteCutEffectSpawnerSpawnNoteCutEffect
    {
        private static bool Prefix(NoteController noteController)
        {
            if (ChromaBehaviour.LightingRegistered && noteController.noteData is CustomNoteData customData)
            {
                dynamic dynData = customData.customData;
                bool? reset = Trees.at(dynData, "_disableNoteDebris");
                if (reset.HasValue && reset == true)
                {
                    return false;
                }
            }

            if (ChromaNoteColorEvent.SavedNoteColours.TryGetValue(noteController, out Color c))
            {
                ChromaColorManager.SetNoteTypeColourOverride(noteController.noteData.noteType, c);
            }

            return true;
        }

        private static void Postfix(NoteController noteController)
        {
            ChromaColorManager.RemoveNoteTypeColourOverride(noteController.noteData.noteType);
        }
    }
}
