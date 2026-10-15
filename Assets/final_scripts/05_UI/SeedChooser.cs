using System.Collections.Generic;
using SocketSave;
using UnityEngine;

public class SeedChooser : MonoBehaviour
{
    public static SeedChooser Instance;

    [Header("Managers")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private LV lv;
    [SerializeField] private LVManager lvManager;
    [SerializeField] private OnlineNetworkClient socketClient;
    [SerializeField] private BattlePlayerList battlePlayerList;
    [SerializeField] private SpectatorList spectatorList;
    [SerializeField] private SeedBank seedBank;
    [SerializeField] private CameraControl cameraControl;

    [Header("UI")]
    public StartButton startButton;
    public List<Transform> Pages = new List<Transform>();
    public UIPlantCardNC ForGetSizeNc;
    public Transform ChangeChooserBtn;
    public Transform CallBtn;
    public Transform LookGroundBtn;

    public bool isPrepare;

    private RectTransform rectTransform;
    private UIPlantCardNC[][] cards;
    private int currPage;

    private bool isMoving;
    private float moveTargetPosX;
    private bool moveIsOut;

    private static readonly PlantType[] LastStandPlants =
    {
        PlantType.SunFlower,
        PlantType.SunShroom,
        PlantType.TwinSunflower,
        PlantType.Heronsbill
    };

    public int CurrPage
    {
        get => currPage;

        set
        {
            if (Pages == null || Pages.Count == 0)
                return;

            int oldPage = currPage;

            if (value >= Pages.Count)
                currPage = 0;
            else if (value < 0)
                currPage = Pages.Count - 1;
            else
                currPage = value;

            if (oldPage >= 0 &&
                oldPage < Pages.Count &&
                oldPage != currPage &&
                Pages[oldPage] != null)
            {
                Pages[oldPage].gameObject.SetActive(false);
            }

            if (Pages[currPage] != null)
            {
                Pages[currPage].gameObject.SetActive(true);
            }
        }
    }

    private void Awake()
    {
        Instance = this;

        rectTransform = GetComponent<RectTransform>();

        InitializeCards();

        InitializePages();
    }

    private void InitializeCards()
    {
        if (Pages == null)
        {
            cards = new UIPlantCardNC[0][];
            return;
        }

        cards = new UIPlantCardNC[Pages.Count][];

        for (int i = 0; i < Pages.Count; i++)
        {
            if (Pages[i] == null)
                continue;

            cards[i] =
                Pages[i].GetComponentsInChildren<UIPlantCardNC>(true);
        }
    }

    private void InitializePages()
    {
        if (Pages == null || Pages.Count == 0)
            return;

        currPage = 0;

        // Desactivar todas las páginas inicialmente.
        for (int i = 0; i < Pages.Count; i++)
        {
            if (Pages[i] != null)
                Pages[i].gameObject.SetActive(false);
        }

        // Activar Page 0.
        if (Pages[0] != null)
            Pages[0].gameObject.SetActive(true);

        // Activar Page 1 para inicializarla y después cerrarla.
        if (Pages.Count > 1 && Pages[1] != null)
        {
            Pages[1].gameObject.SetActive(true);
            Pages[1].gameObject.SetActive(false);
        }

        // Garantizar que Page 0 sea la página seleccionada.
        currPage = 0;

        if (Pages[0] != null)
            Pages[0].gameObject.SetActive(true);
    }

    private void OnEnable()
    {
        InitializePages();
    }

    private void OnDisable()
    {
        isMoving = false;
    }

    private void Update()
    {
        UpdateMovement();
    }

    private void UpdateMovement()
    {
        if (!isMoving || rectTransform == null)
            return;

        Vector3 position = rectTransform.position;

        position.x = Mathf.MoveTowards(
            position.x,
            moveTargetPosX,
            4000f * Time.deltaTime
        );

        rectTransform.position = position;

        if (Mathf.Abs(moveTargetPosX - position.x) > 0.1f)
            return;

        position.x = moveTargetPosX;
        rectTransform.position = position;

        isMoving = false;

        if (moveIsOut)
        {
            ClearAllChoose();
            gameObject.SetActive(false);
        }
    }

    private void StartMoveTo(float targetPosX, bool isOut)
    {
        if (rectTransform == null)
        {
            Debug.LogError(
                "[SeedChooser] StartMoveTo BLOQUEADO: rectTransform == null"
            );

            return;
        }

        moveTargetPosX = targetPosX;
        moveIsOut = isOut;
        isMoving = true;

        Debug.Log(
            $"[SeedChooser] Movimiento iniciado | target={targetPosX} | isOut={isOut}"
        );
    }

    public UIPlantCardNC GetCardInfo(PlantType plantType)
    {
        if (plantType == PlantType.Nope || cards == null)
            return null;

        for (int i = 0; i < cards.Length; i++)
        {
            UIPlantCardNC[] pageCards = cards[i];

            if (pageCards == null)
                continue;

            for (int j = 0; j < pageCards.Length; j++)
            {
                if (pageCards[j] != null &&
                    pageCards[j].CardPlantType == plantType)
                {
                    return pageCards[j];
                }
            }
        }

        return null;
    }

    public UIPlantCardNC[] GetCardInfos(int pageIndex)
    {
        if (cards == null ||
            pageIndex < 0 ||
            pageIndex >= cards.Length)
        {
            return null;
        }

        return cards[pageIndex];
    }

    public UIPlantCardNC GetCardInfo(int cardId)
    {
        if (cards == null)
            return null;

        int count = 0;

        for (int i = 0; i < cards.Length; i++)
        {
            UIPlantCardNC[] pageCards = cards[i];

            if (pageCards == null)
                continue;

            for (int j = 0; j < pageCards.Length; j++)
            {
                if (pageCards[j] == null)
                    continue;

                count++;

                if (count == cardId)
                    return pageCards[j];
            }
        }

        return null;
    }

    public PlantType GetCardType(int cardId)
    {
        UIPlantCardNC card = GetCardInfo(cardId);

        return card != null
            ? card.CardPlantType
            : PlantType.Nope;
    }

    public void ClearAllChoose()
    {
        if (cards == null)
            return;

        for (int i = 0; i < cards.Length; i++)
        {
            UIPlantCardNC[] pageCards = cards[i];

            if (pageCards == null)
                continue;

            for (int j = 0; j < pageCards.Length; j++)
            {
                if (pageCards[j] != null)
                    pageCards[j].IsChoosed = false;
            }
        }
    }

    public void ResetThis()
    {
        if (Pages == null || Pages.Count == 0)
            return;

        InitializePages();

        isMoving = false;
        moveIsOut = false;

        if (startButton != null)
            startButton.gameObject.SetActive(true);

        if (CallBtn != null)
            CallBtn.gameObject.SetActive(true);

        if (LookGroundBtn != null)
            LookGroundBtn.gameObject.SetActive(true);

        if (rectTransform != null)
        {
            rectTransform.anchoredPosition =
                new Vector2(-600f, -45f);
        }

        if (cards == null)
            return;

        for (int i = 0; i < cards.Length; i++)
        {
            UIPlantCardNC[] pageCards = cards[i];

            if (pageCards == null)
                continue;

            for (int j = 0; j < pageCards.Length; j++)
            {
                UIPlantCardNC card = pageCards[j];

                if (card == null ||
                    card.CardPlantType == PlantType.Nope)
                {
                    continue;
                }

                card.isForCreate = false;

                if (gameManager != null &&
                    gameManager.LocalPlayerSave != null)
                {
                    card.IsUnLock =
                        gameManager.LocalPlayerSave
                            .UnlockedPlants
                            .Contains(card.CardPlantType);
                }

                if (lv != null &&
                    lv.LvSpStates.Contains(LVSpState.LastStand) &&
                    System.Array.IndexOf(
                        LastStandPlants,
                        card.CardPlantType
                    ) >= 0)
                {
                    card.IsChoosed = true;
                }
            }
        }
    }

    public void StartMove()
    {
        if (gameManager == null ||
            spectatorList == null ||
            startButton == null ||
            seedBank == null)
        {
            Debug.LogError(
                "[SeedChooser] StartMove BLOQUEADO: falta una referencia."
            );

            return;
        }

        if (gameManager.isClient)
        {
            if (spectatorList.LocalIsSpectator)
            {
                isPrepare = true;
                startButton.StartText.text = "Empezar";
            }
            else
            {
                isPrepare = false;

                startButton.StartText.text =
                    isPrepare ? "Cancelar" : "Listo";
            }
        }
        else
        {
            isPrepare = spectatorList.LocalIsSpectator;
            startButton.StartText.text = "Empezar";
        }

        if (Camera.main == null || rectTransform == null)
        {
            Debug.LogError(
                "[SeedChooser] StartMove BLOQUEADO: Camera.main o RectTransform es null."
            );

            return;
        }

        float targetX =
            Camera.main.WorldToScreenPoint(
                seedBank.transform.position +
                new Vector3(5f, 0f)
            ).x;

        StartMoveTo(targetX, false);
    }

    public void StartRunLv(bool synClient = false)
    {
        Debug.Log(
            $"[SeedChooser] StartRunLv RECIBIDO | synClient={synClient}"
        );

        if (gameManager == null)
        {
            Debug.LogError(
                "[SeedChooser] StartRunLv BLOQUEADO: gameManager == null"
            );

            return;
        }

        if (lvManager == null)
        {
            Debug.LogError(
                "[SeedChooser] StartRunLv BLOQUEADO: lvManager == null"
            );

            return;
        }

        Debug.Log(
            $"[SeedChooser] gameManager.isClient={gameManager.isClient}"
        );

        bool canStart = !gameManager.isClient || synClient;

        Debug.Log($"[SeedChooser] canStart={canStart}");

        if (!canStart)
        {
            Debug.Log(
                "[SeedChooser] NO inicia porque es cliente y synClient=false."
            );

            return;
        }

        Debug.Log("[SeedChooser] Iniciando movimiento hacia -800");

        StartMoveTo(-800f, true);

        Debug.Log("[SeedChooser] Movimiento iniciado.");

        Debug.Log("[SeedChooser] Llamando lvManager.StartRunLv()...");

        lvManager.StartRunLv();

        Debug.Log("[SeedChooser] lvManager.StartRunLv() EJECUTADO.");
    }

    public void Prepare()
    {
        if (startButton == null ||
            socketClient == null ||
            battlePlayerList == null ||
            gameManager == null)
        {
            Debug.LogError(
                "[SeedChooser] Prepare BLOQUEADO: falta una referencia."
            );

            return;
        }

        isPrepare = !isPrepare;

        Debug.Log($"[SeedChooser] Prepare -> isPrepare={isPrepare}");

        startButton.StartText.text =
            isPrepare ? "取消准备" : "准备";

        SelectPrepare selectPrepare = new SelectPrepare
        {
            isPrepare = isPrepare
        };

        Debug.Log("[SeedChooser] Enviando SelectPrepare al servidor.");

        socketClient.SelectPrepare(selectPrepare);

        Debug.Log("[SeedChooser] SelectPrepare enviado.");

        if (gameManager.LocalPlayerSave != null)
        {
            battlePlayerList.UpdateState(
                gameManager.LocalPlayerSave.playerName,
                isPrepare
            );

            Debug.Log(
                $"[SeedChooser] Estado local actualizado: {gameManager.LocalPlayerSave.playerName} -> {isPrepare}"
            );
        }
    }

    public void OpenForCreate()
    {
        if (lvManager == null || !lvManager.GameIsStart)
            return;

        gameObject.SetActive(true);

        if (startButton != null)
            startButton.gameObject.SetActive(false);

        if (CallBtn != null)
            CallBtn.gameObject.SetActive(false);

        if (LookGroundBtn != null)
            LookGroundBtn.gameObject.SetActive(false);

        if (Camera.main != null &&
            cameraControl != null &&
            rectTransform != null)
        {
            rectTransform.position =
                Camera.main.WorldToScreenPoint(
                    cameraControl.transform.position
                );
        }

        if (ChangeChooserBtn != null)
            ChangeChooserBtn.gameObject.SetActive(false);

        if (cards == null)
            return;

        for (int i = 0; i < cards.Length; i++)
        {
            UIPlantCardNC[] pageCards = cards[i];

            if (pageCards == null)
                continue;

            for (int j = 0; j < pageCards.Length; j++)
            {
                UIPlantCardNC card = pageCards[j];

                if (card == null ||
                    card.CardPlantType == PlantType.Nope)
                {
                    continue;
                }

                card.IsUnLock = true;
                card.isForCreate = true;
            }
        }
    }

    public void ChooseOver()
    {
        gameObject.SetActive(false);
        ResetThis();
    }
}