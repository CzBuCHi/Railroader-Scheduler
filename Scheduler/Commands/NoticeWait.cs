using System;
using System.Collections;
using Newtonsoft.Json;
using System.Collections.Generic;
using Model;
using Scheduler.Utility;
using UI.Builder;
using UnityEngine;
using GalaSoft.MvvmLight.Messaging;
using Game.Notices;
using Scheduler.Messages;

namespace Scheduler.Commands;

/// <summary> Shows notice to player and wait until player dismiss it. </summary>
/// <param name="message">Notice message</param>
public class NoticeWait(string message): ICommand
{
    public string DisplayText => $"Wait for notice {Message}.";

    public string Message { get; } = message;
}

public sealed class NoticeWaitManager : CommandManager<NoticeWait>
{
    public override IEnumerator Execute(Dictionary<string, object> state) {
        var locomotive = (BaseLocomotive)state["locomotive"]!;
        var entityReference = new EntityReference(EntityType.Car, locomotive.id!);
        var key = "Scheduler:" + Guid.NewGuid();
        NoticeManager.Shared.PostEphemeral(entityReference, key, Command!.Message);

        var dismissed = false;
        Messenger.Default.Register<NoticeDismissed>(this, message => dismissed = message.Key == key);
        yield return new WaitUntil(() => dismissed);
        Messenger.Default.Unregister(this);
    }

    private string? _Message;

    public override void SerializeProperties(JsonWriter writer) {
        writer.WritePropertyName(nameof(NoticeWait.Message));
        writer.WriteValue(Command!.Message);
    }

    protected override void ReadProperty(string propertyName, JsonReader reader, JsonSerializer serializer) {
        if (propertyName == nameof(NoticeWait.Message)) {
            _Message = serializer.Deserialize<string>(reader);
        }
    }

    protected override object TryCreateCommand() {
        if (string.IsNullOrEmpty(_Message!)) {
            return "Missing mandatory property 'Message'.";
        }

        return new NoticeWait(_Message!);
    }

    public override void BuildPanel(UIPanelBuilder builder, BaseLocomotive locomotive) {
        builder.AddField("Message",
            builder.AddInputField(_Message ?? "", o => _Message = o, "Notification message", 50)!
        );
    }
}
