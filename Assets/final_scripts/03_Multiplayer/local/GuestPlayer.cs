using System;
using System.Collections.Generic;
using UnityEngine;

public class GuestPlayer
{
    public readonly string Name;
    public readonly Color Color;
    public readonly GuestInput Input;

    public PlantCard SelectedCard { get; private set; }
    public Grid CurrentGrid => currGrid;

    public float CellSize = 1.3f;
    public float CursorSizeFactor = 0.9f;
    public Vector2 CursorOffset = Vector2.zero;

    public event Action<GuestPlayer, PlantCard, Grid> ConfirmRequested;

    private const int CursorSortingOrder = 3000;

    private readonly int startOffset;
    private readonly GameObject cursorVisualTemplate;
    private readonly TextMesh cursorTextTemplate;
    private readonly bool tintText;
    private readonly bool showPlayerName;

    private Grid currGrid;

    private PlantBase ghost;
    private PlantType ghostType = PlantType.Nope;
    private bool ghostImitater;

    private GameObject cursorVisualInstance;
    private SpriteRenderer cursorRenderer;
    private TextMesh cursorText;

    public GuestPlayer(
        string name,
        Color color,
        GuestInput input,
        int startOffset,
        GameObject cursorVisual,
        TextMesh cursorText,
        float cursorSizeFactor,
        Vector2 cursorOffset,
        bool tintTextWithPlayerColor,
        bool showPlayerName)
    {
        Name = name;
        Color = color;
        Input = input;

        this.startOffset = startOffset;
        cursorVisualTemplate = cursorVisual;
        cursorTextTemplate = cursorText;
        this.CursorSizeFactor = cursorSizeFactor;
        this.CursorOffset = cursorOffset;
        tintText = tintTextWithPlayerColor;
        this.showPlayerName = showPlayerName;
    }

    public void Tick(List<Grid> grids, List<PlantCard> cards)
    {
        if (grids == null || grids.Count == 0)
            return;

        if (currGrid == null || !grids.Contains(currGrid))
            currGrid = PickStartGrid(grids);

        GuestInputState s = Input.Poll();

        if (s.Move != Vector2Int.zero)
            currGrid = Neighbor(grids, currGrid, s.Move);

        if (SelectedCard != null && !cards.Contains(SelectedCard))
            SelectedCard = null;

        if (s.Next)
            Cycle(cards, +1);

        if (s.Prev)
            Cycle(cards, -1);

        if (s.Cancel)
            SelectedCard = null;

        if (s.Confirm && SelectedCard != null)
            ConfirmRequested?.Invoke(
                this,
                SelectedCard,
                currGrid
            );

        UpdateVisuals();
    }

    public void Clear()
    {
        ClearGhost();

        if (cursorVisualInstance != null)
            cursorVisualInstance.SetActive(false);
    }

    public void ClearSelectedCard()
    {
        SelectedCard = null;
        ClearGhost();
    }

    public void Destroy()
    {
        Clear();

        if (cursorVisualInstance != null)
            UnityEngine.Object.Destroy(cursorVisualInstance);

        cursorVisualInstance = null;
        cursorRenderer = null;
        cursorText = null;
    }

    private void Cycle(List<PlantCard> cards, int dir)
    {
        if (cards == null || cards.Count == 0)
        {
            SelectedCard = null;
            return;
        }

        int i =
            SelectedCard == null
                ? -1
                : cards.IndexOf(SelectedCard);

        if (i < 0)
            i = dir > 0 ? -1 : 0;

        i =
            ((i + dir) % cards.Count + cards.Count) %
            cards.Count;

        SelectedCard = cards[i];
    }

    private Grid PickStartGrid(List<Grid> grids)
    {
        Vector2 center = Vector2.zero;
        int validCount = 0;

        for (int i = 0; i < grids.Count; i++)
        {
            Grid grid = grids[i];

            if (grid == null)
                continue;

            center += grid.Position;
            validCount++;
        }

        if (validCount == 0)
            return null;

        center /= validCount;

        List<Grid> sorted =
            new List<Grid>(grids);

        sorted.RemoveAll(
            grid => grid == null
        );

        sorted.Sort(
            (a, b) =>
                (a.Position - center)
                    .sqrMagnitude
                    .CompareTo(
                        (b.Position - center)
                            .sqrMagnitude
                    )
        );

        if (sorted.Count == 0)
            return null;

        int index =
            Mathf.Clamp(
                startOffset * 2,
                0,
                sorted.Count - 1
            );

        return sorted[index];
    }

    private static Grid Neighbor(
        List<Grid> grids,
        Grid from,
        Vector2Int dir)
    {
        if (from == null)
            return null;

        Vector2 d =
            new Vector2(
                dir.x,
                dir.y
            ).normalized;

        Grid best = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < grids.Count; i++)
        {
            Grid grid = grids[i];

            if (grid == null || grid == from)
                continue;

            Vector2 delta =
                grid.Position -
                from.Position;

            float along =
                Vector2.Dot(
                    delta,
                    d
                );

            if (along < 0.2f)
                continue;

            float perp =
                Mathf.Abs(
                    delta.x * d.y -
                    delta.y * d.x
                );

            float score =
                along +
                perp * 3f;

            if (score < bestScore)
            {
                bestScore = score;
                best = grid;
            }
        }

        return best ?? from;
    }

    private void UpdateVisuals()
    {
        if (currGrid == null)
            return;

        EnsureCursorVisual();

        if (cursorVisualInstance == null)
            return;

        cursorVisualInstance.SetActive(true);

        cursorVisualInstance.transform.position =
            currGrid.Position +
            CursorOffset;

        ApplyCursorScale();
        UpdateCursorText();
        UpdateGhost();
    }

    private void UpdateCursorText()
    {
        if (cursorText == null)
            return;

        if (!showPlayerName)
        {
            cursorText.text = string.Empty;
        }
        else
        {
            cursorText.text =
                SelectedCard == null
                    ? Name
                    : Name +
                      "\n" +
                      SelectedCard.CardPlantType;
        }

        if (tintText)
            cursorText.color = Color;
    }

    private void UpdateGhost()
    {
        if (
            SelectedCard == null ||
            SelectedCard.CardPlantType == PlantType.Nope
        )
        {
            ClearGhost();
            return;
        }

        bool changed =
            ghost == null ||
            ghostType !=
                SelectedCard.CardPlantType ||
            ghostImitater !=
                SelectedCard.isImitater;

        if (changed)
        {
            ClearGhost();

            if (PlantManager.Instance == null)
                return;

            ghost =
                PlantManager.Instance.GetNewPlant(
                    SelectedCard.CardPlantType
                );

            if (ghost == null)
                return;

            ghostType =
                SelectedCard.CardPlantType;

            ghostImitater =
                SelectedCard.isImitater;

            ghost.transform.SetParent(
                PlantManager.Instance.transform
            );

            ghost.InitForCreate(
                true,
                currGrid,
                ghostImitater
            );
        }
        else
        {
            ghost.UpdateForCreate(
                currGrid
            );
        }
    }

    private void ClearGhost()
    {
        if (ghost != null)
        {
            ghost.Dead(
                false,
                0f,
                true,
                false
            );

            ghost = null;
        }

        ghostType = PlantType.Nope;
        ghostImitater = false;
    }

    private void ApplyCursorScale()
    {
        if (
            cursorVisualInstance == null ||
            cursorRenderer == null ||
            cursorRenderer.sprite == null
        )
        {
            return;
        }

        float width =
            cursorRenderer.sprite.bounds.size.x;

        if (width <= 0.0001f)
            return;

        float scale =
            CellSize *
            CursorSizeFactor /
            width;

        cursorVisualInstance.transform.localScale =
            new Vector3(
                scale,
                scale,
                1f
            );
    }

    private void EnsureCursorVisual()
    {
        if (cursorVisualInstance != null)
            return;

        if (cursorVisualTemplate == null)
        {
            Debug.LogWarning(
                "[Guest] " +
                Name +
                " no tiene un GameObject de cursor asignado."
            );

            return;
        }

        Transform parent = null;

        if (PlantManager.Instance != null)
            parent =
                PlantManager.Instance.transform;

        cursorVisualInstance =
            UnityEngine.Object.Instantiate(
                cursorVisualTemplate,
                parent
            );

        cursorVisualInstance.name =
            "GuestCursor_" +
            Name;

        cursorVisualInstance.transform.localScale =
            Vector3.one;

        cursorRenderer =
            cursorVisualInstance.GetComponent<SpriteRenderer>();

        if (cursorRenderer == null)
        {
            cursorRenderer =
                cursorVisualInstance
                    .GetComponentInChildren<SpriteRenderer>(
                        true
                    );
        }

        if (cursorRenderer != null)
        {
            cursorRenderer.sortingOrder =
                CursorSortingOrder;
        }
        else
        {
            Debug.LogWarning(
                "[Guest] El cursor de " +
                Name +
                " no tiene SpriteRenderer."
            );
        }

        cursorText =
            cursorVisualInstance.GetComponent<TextMesh>();

        if (cursorText == null)
        {
            cursorText =
                cursorVisualInstance
                    .GetComponentInChildren<TextMesh>(
                        true
                    );
        }

        if (cursorText == null)
        {
            if (cursorTextTemplate != null)
            {
                Debug.LogWarning(
                    "[Guest] El cursor de " +
                    Name +
                    " no contiene TextMesh. " +
                    "El TextMesh asignado como plantilla " +
                    "no se crea como objeto separado."
                );
            }
            else
            {
                Debug.LogWarning(
                    "[Guest] El cursor de " +
                    Name +
                    " no contiene TextMesh."
                );
            }
        }
        else
        {
            MeshRenderer meshRenderer =
                cursorText.GetComponent<MeshRenderer>();

            if (meshRenderer != null)
                meshRenderer.sortingOrder =
                    CursorSortingOrder + 1;
        }

        cursorVisualInstance.SetActive(true);
    }
}