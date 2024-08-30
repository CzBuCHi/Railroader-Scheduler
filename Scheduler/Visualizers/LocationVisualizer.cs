using System.Collections;
using Track;
using Track.Signals;
using UnityEngine;

namespace Scheduler.Visualizers;

internal sealed class LocationVisualizer : MonoBehaviour
{
    private static readonly Material _LineMaterial = new(Shader.Find("Universal Render Pipeline/Lit")!);

    private LineRenderer? _LineRenderer;

    private LineRenderer CreateLineRenderer() {
        var lineRenderer = gameObject.AddComponent<LineRenderer>()!;
        lineRenderer.material = _LineMaterial;
        lineRenderer.material.color = Color.green;
        lineRenderer.startWidth = 0.05f;
        lineRenderer.positionCount = 5;
        lineRenderer.useWorldSpace = false;
        lineRenderer.SetPosition(0, new Vector3(-0.2f, 0.5f, 0));
        lineRenderer.SetPosition(1, new Vector3(0, 0, 0));
        lineRenderer.SetPosition(2, new Vector3(0, 7, 0));
        lineRenderer.SetPosition(3, new Vector3(0, 0, 0));
        lineRenderer.SetPosition(4, new Vector3(0.2f, 0.5f, 0));
        lineRenderer.enabled = true;
        return lineRenderer;
    }

    public void Show(Location location, Color color) {
        Vector3 position;
        if (location.Distance == 0) {
            position = (location.EndIsA ? location.segment.a! : location.segment.b!).transform.localPosition;
        } else {
            var p = location.DistanceTo(location.segment.a!) / location.segment.GetLength();
            position = location.segment.Curve.GetPoint(p);
        }

        gameObject.transform.SetParent(Graph.Shared.transform);
        gameObject.transform.localPosition = position;
        gameObject.transform.localRotation = Quaternion.identity;

        _LineRenderer ??= CreateLineRenderer();
        _LineRenderer.material.color = color;
        _LineRenderer.enabled = true;
        StartCoroutine(Routine());
    }

    private IEnumerator Routine() {
        yield return new WaitForSecondsRealtime(10f);
        _LineRenderer!.enabled = false;
    }
}
