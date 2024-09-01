using System;
using GalaSoft.MvvmLight.Messaging;
using Game.Notices;
using HarmonyLib;
using JetBrains.Annotations;
using Scheduler.Messages;

namespace Scheduler.HarmonyPatches;

[PublicAPI]
[HarmonyPatch]
public static class NoticeManagerPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(NoticeManager), nameof(DismissRow))]
    public static void DismissRow(string key) {
        var parts = key.Split(["//"], StringSplitOptions.None);
        if (parts.Length == 3) {
            Messenger.Default.Send(new NoticeDismissed { Key = parts[2] });
        }
    }
}
