using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adds invisible press targets on the left and right of each MCP turn knob.
/// Each target is a square whose side equals that knob's press width.
/// Left repeats the decrease control, right repeats the increase control.
/// </summary>
public static class TurnKnobSideHits
{
    const string PadName = "KnobSideHit";

    class Knob
    {
        public RectTransform Root;
        public RectTransform Panel;
        public LongPressEventTrigger Negative;
        public LongPressEventTrigger Positive;
    }

    class Piece
    {
        public Rect Rect;
        public LongPressEventTrigger Source;
        public RectTransform Panel;
        public bool Priority;
    }

    public static void Create(Transform mcpRoot)
    {
        if (mcpRoot == null)
            return;

        var knobs = CollectKnobs(mcpRoot);
        if (knobs.Count == 0)
            return;

        var pieces = new List<Piece>();
        foreach (var knob in knobs)
        {
            if (knob.Panel == null || knob.Panel.Find(PadName) != null)
                continue;

            var knobRect = WorldRectIn(knob.Panel, knob.Root);
            AddSide(pieces, knob, knobRect, left: true);
            AddSide(pieces, knob, knobRect, left: false);
        }

        for (var pass = 0; pass < 2; pass++)
        {
            var wantPriority = pass == 1;
            foreach (var piece in pieces)
            {
                if (piece.Priority != wantPriority)
                    continue;
                if (piece.Source == null || piece.Rect.width < 1f)
                    continue;
                CreatePad(piece);
            }
        }
    }

    static List<Knob> CollectKnobs(Transform mcpRoot)
    {
        var knobs = new List<Knob>();
        foreach (var linker in mcpRoot.GetComponentsInChildren<KnobButtonLinker>(true))
        {
            var root = linker.transform.parent as RectTransform;
            if (root == null)
                continue;

            Knob knob = null;
            for (var i = 0; i < knobs.Count; i++)
            {
                if (knobs[i].Root == root)
                {
                    knob = knobs[i];
                    break;
                }
            }

            if (knob == null)
            {
                knob = new Knob
                {
                    Root = root,
                    Panel = root.parent as RectTransform
                };
                knobs.Add(knob);
            }

            if (linker.IsPositive)
                knob.Positive = linker.Trigger;
            else
                knob.Negative = linker.Trigger;
        }

        return knobs;
    }

    static bool IsAltitude(Knob knob)
    {
        return knob.Root != null && knob.Root.name.StartsWith("A ");
    }

    static void AddSide(List<Piece> pieces, Knob knob, Rect knobRect, bool left)
    {
        var source = left ? knob.Negative : knob.Positive;
        if (source == null || !source.isActiveAndEnabled)
            return;

        var side = knobRect.width;
        if (side < 1f)
            return;

        var yMin = knobRect.center.y - side * 0.5f;
        var xMin = left ? knobRect.xMin - side : knobRect.xMax;
        pieces.Add(new Piece
        {
            Rect = new Rect(xMin, yMin, side, side),
            Source = source,
            Panel = knob.Panel,
            Priority = left && IsAltitude(knob)
        });
    }

    static void CreatePad(Piece piece)
    {
        var panel = piece.Panel;
        if (panel == null)
            return;

        var go = new GameObject(PadName, typeof(RectTransform));
        go.layer = panel.gameObject.layer;
        go.transform.SetParent(panel, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(piece.Rect.width, piece.Rect.height);
        rect.anchoredPosition = piece.Rect.center;

        if (piece.Priority)
        {
            var parentCanvas = panel.GetComponentInParent<Canvas>();
            var overlay = go.AddComponent<Canvas>();
            overlay.overrideSorting = true;
            overlay.sortingOrder = (parentCanvas != null ? parentCanvas.sortingOrder : 0) + 50;
            go.AddComponent<GraphicRaycaster>();
        }

        var image = go.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = true;
        if (piece.Priority)
            go.transform.SetAsLastSibling();

        var trigger = go.AddComponent<LongPressEventTrigger>();
        trigger.CopyTimingFrom(piece.Source);
        var source = piece.Source;
        trigger.onClick.AddListener(() => source.onClick.Invoke());
    }

    static Rect WorldRectIn(RectTransform space, RectTransform target)
    {
        var corners = new Vector3[4];
        target.GetWorldCorners(corners);
        var x0 = float.PositiveInfinity;
        var y0 = float.PositiveInfinity;
        var x1 = float.NegativeInfinity;
        var y1 = float.NegativeInfinity;
        for (var i = 0; i < 4; i++)
        {
            Vector2 point = space.InverseTransformPoint(corners[i]);
            x0 = Mathf.Min(x0, point.x);
            y0 = Mathf.Min(y0, point.y);
            x1 = Mathf.Max(x1, point.x);
            y1 = Mathf.Max(y1, point.y);
        }

        return Rect.MinMaxRect(x0, y0, x1, y1);
    }
}
