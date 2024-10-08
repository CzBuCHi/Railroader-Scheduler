﻿namespace Scheduler.HarmonyPatches;

using System;
using HarmonyLib;
using Track;

[HarmonyPatch]
public static class GraphPatches {

    [HarmonyReversePatch]
    [HarmonyPatch(typeof(Graph), nameof(SegmentsReachableFrom))]
    public static void SegmentsReachableFrom(this Graph __instance, TrackSegment segment, TrackSegment.End end, out TrackSegment normal, out TrackSegment reversed) {
        throw new NotImplementedException("This is a stub");
    }

}