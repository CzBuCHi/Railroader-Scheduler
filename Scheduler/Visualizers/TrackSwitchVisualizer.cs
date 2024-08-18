using System.Text;
using Game.Messages;
using Helpers;
using JetBrains.Annotations;
using Track;
using UnityEngine;

namespace Scheduler.Visualizers;

/// <summary> Used by editor of <see cref="SetSwitch"/> command. </summary>
internal sealed class TrackSwitchVisualizer : MonoBehaviour, IPickable
{
    private static readonly Material _LineMaterial = new(Shader.Find("Universal Render Pipeline/Lit")!);

    private TrackNode _TrackNode = null!;

    private LineRenderer? _LineRenderer;

    public float MaxPickDistance => 200f;

    public int Priority => 1;

    public TooltipInfo TooltipInfo => BuildTooltipInfo();
    public PickableActivationFilter ActivationFilter => PickableActivationFilter.PrimaryOnly;

    private TooltipInfo BuildTooltipInfo() {
        var sb = new StringBuilder();
        sb.AppendLine($"ID: {_TrackNode.id}");
        sb.AppendLine($"Pos: {_TrackNode.transform.localPosition}");
        sb.AppendLine($"Rot: {_TrackNode.transform.localEulerAngles}");
        return new TooltipInfo($"Node {_TrackNode.id}", sb.ToString());
    }

    [UsedImplicitly]
    public void Start() {
        _TrackNode = transform.parent.GetComponent<TrackNode>()!;

        transform.localPosition = Vector3.zero;
        transform.localEulerAngles = Vector3.zero;

        gameObject.layer = Layers.Clickable;

        _LineRenderer = gameObject.AddComponent<LineRenderer>();
        _LineRenderer.material = _LineMaterial;
        _LineRenderer.startWidth = 0.05f;
        _LineRenderer.positionCount = 5;
        _LineRenderer.useWorldSpace = false;

        const float sizeX = -0.2f;
        const float sizeY = -0.4f;
        const float sizeZ = 0.3f;

        _LineRenderer.SetPosition(0, new Vector3(-sizeX, 0, sizeY));
        _LineRenderer.SetPosition(1, new Vector3(0, 0, sizeZ));
        _LineRenderer.SetPosition(2, new Vector3(sizeX, 0, sizeY));
        _LineRenderer.SetPosition(3, new Vector3(0, 0, -sizeZ));
        _LineRenderer.SetPosition(4, new Vector3(-sizeX, 0, sizeY));

        var boxCollider = gameObject.AddComponent<BoxCollider>();
        boxCollider.size = new Vector3(0.4f, 0.4f, 0.8f);
    }

    [UsedImplicitly]
    public void Update() {
        _LineRenderer!.enabled = SchedulerPlugin.ShowTrackSwitchVisualizers;
        _LineRenderer.material!.color = SchedulerPlugin.SelectedSwitch == _TrackNode ? Color.magenta : Color.cyan;
    }

    public void Activate(PickableActivateEvent evt) {
        SchedulerPlugin.SelectedSwitch = _TrackNode;
    }

    public void Deactivate() {
    }
}
