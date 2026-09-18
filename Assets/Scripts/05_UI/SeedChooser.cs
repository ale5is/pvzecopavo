using System.Collections;
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
    [SerializeField] private SocketClient socketClient;
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
            if (Pages.Count == 0)
                return;

            int oldPage = currPage;

            if (value >= Pages.Count)
                currPage = 0;
            else if (value < 0)
                currPage = Pages.Count - 1;
            else
                currPage = value;

            if (oldPage >= 0 && oldPage < Pages.Count)
                Pages[oldPage].gameObject.SetActive(false);

            Pages[currPage].gameObject.SetActive(true);
        }
    }

    private void Awake()
    {
        Instance = this;
        rectTransform = GetComponent<RectTransform>();

        cards = new UIPlantCardNC[Pages.Count][];

        for (int i = 0; i < Pages.Count; i++)
        {
            if (Pages[i] == null)
                continue;

            cards[i] =
                Pages[i].GetComponentsInChildren<UIPlantCardNC>(true);

            Pages[i].gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        CurrPage = 0;

        StartCoroutine(
            InitializeSecondPage()
        );
    }

    private IEnumerator InitializeSecondPage()
    {
        if (Pages.Count <= 1 ||
            Pages[1] == null)
        {
            yield break;
        }

        Pages[1].gameObject.SetActive(true);

        yield return null;
        yield return null;

        Pages[1].gameObject.SetActive(false);
    }

    public UIPlantCardNC GetCardInfo(PlantType plantType)
    {
        if (plantType == PlantType.Nope)
            return null;

        for (int i = 0; i < cards.Length; i++)
        {
            UIPlantCardNC[] pageCards = cards[i];

            if (pageCards == null)
                continue;

            for (int j = 0; j < pageCards.Length; j++)
            {
                if (pageCards[j].CardPlantType == plantType)
                    return pageCards[j];
            }
        }

        return null;
    }

    public UIPlantCardNC[] GetCardInfos(int pageIndex)
    {
        if (pageIndex < 0 ||
            pageIndex >= cards.Length)
        {
            return null;
        }

        return cards[pageIndex];
    }

    public UIPlantCardNC GetCardInfo(int cardId)
    {
        int count = 0;

        for (int i = 0; i < cards.Length; i++)
        {
            UIPlantCardNC[] pageCards = cards[i];

            if (pageCards == null)
                continue;

            for (int j = 0; j < pageCards.Length; j++)
            {
                count++;

                if (count == cardId)
                    return pageCards[j];
            }
        }

        return null;
    }

    public PlantType GetCardType(int cardId)
    {
        UIPlantCardNC card =
            GetCardInfo(cardId);

        return card != null
            ? card.CardPlantType
            : PlantType.Nope;
    }

    public void ClearAllChoose()
    {
        for (int i = 0; i < cards.Length; i++)
        {
            UIPlantCardNC[] pageCards = cards[i];

            if (pageCards == null)
                continue;

            for (int j = 0; j < pageCards.Length; j++)
            {
                pageCards[j].IsChoosed = false;
            }
        }
    }

    public void ResetThis()
    {
        if (Pages.Count == 0)
            return;

        CurrPage = 0;
        StopAllCoroutines();

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

        for (int i = 0; i < cards.Length; i++)
        {
            UIPlantCardNC[] pageCards = cards[i];

            if (pageCards == null)
                continue;

            for (int j = 0; j < pageCards.Length; j++)
            {
                UIPlantCardNC card =
                    pageCards[j];

                if (card.CardPlantType == PlantType.Nope)
                    continue;

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
                    lv.LvSpStates.Contains(
                        LVSpState.LastStand) &&
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
                startButton.StartText.text = "旁观中";
            }
            else
            {
                isPrepare = false;
                startButton.StartText.text =
                    isPrepare
                        ? "取消准备"
                        : "准备";
            }
        }
        else
        {
            isPrepare =
                spectatorList.LocalIsSpectator;

            startButton.StartText.text =
                "开始战斗";
        }

        if (Camera.main == null ||
            rectTransform == null)
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

        StartCoroutine(
            DoMove(targetX, false)
        );
    }

    public void StartRunLv(
        bool synClient = false)
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

        bool canStart =
            !gameManager.isClient ||
            synClient;

        Debug.Log(
            $"[SeedChooser] canStart={canStart}"
        );

        if (!canStart)
        {
            Debug.Log(
                "[SeedChooser] NO inicia porque es cliente y synClient=false."
            );

            return;
        }

        Debug.Log(
            "[SeedChooser] Iniciando DoMove(-800, true)"
        );

        StartCoroutine(
            DoMove(-800f, true)
        );

        Debug.Log(
            "[SeedChooser] DoMove iniciado."
        );

        Debug.Log(
            "[SeedChooser] Llamando lvManager.StartRunLv()..."
        );

        lvManager.StartRunLv();

        Debug.Log(
            "[SeedChooser] lvManager.StartRunLv() EJECUTADO."
        );
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

        Debug.Log(
            $"[SeedChooser] Prepare -> isPrepare={isPrepare}"
        );

        startButton.StartText.text =
            isPrepare
                ? "取消准备"
                : "准备";

        SelectPrepare selectPrepare =
            new SelectPrepare
            {
                isPrepare = isPrepare
            };

        Debug.Log(
            "[SeedChooser] Enviando SelectPrepare al servidor."
        );

        socketClient.SelectPrepare(
            selectPrepare
        );

        Debug.Log(
            "[SeedChooser] SelectPrepare enviado."
        );

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

    private IEnumerator DoMove(
        float targetPosX,
        bool isOut)
    {
        if (rectTransform == null)
        {
            Debug.LogError(
                "[SeedChooser] DoMove BLOQUEADO: rectTransform == null"
            );

            yield break;
        }

        Debug.Log(
            $"[SeedChooser] DoMove iniciado | target={targetPosX} | actual={rectTransform.position.x} | isOut={isOut}"
        );

        while (Mathf.Abs(
            targetPosX -
            rectTransform.position.x
        ) > 0.1f)
        {
            Vector3 position =
                rectTransform.position;

            position.x =
                Mathf.MoveTowards(
                    position.x,
                    targetPosX,
                    4000f *
                    Time.deltaTime
                );

            rectTransform.position =
                position;

            yield return null;
        }

        Vector3 finalPosition =
            rectTransform.position;

        finalPosition.x =
            targetPosX;

        rectTransform.position =
            finalPosition;

        Debug.Log(
            $"[SeedChooser] DoMove TERMINADO | posición={rectTransform.position.x}"
        );

        if (isOut)
        {
            Debug.Log(
                "[SeedChooser] DoMove -> limpiando selección y desactivando SeedChooser."
            );

            ClearAllChoose();

            gameObject.SetActive(false);

            Debug.Log(
                "[SeedChooser] SeedChooser desactivado."
            );
        }
    }

    public void OpenForCreate()
    {
        if (lvManager == null ||
            !lvManager.GameIsStart)
        {
            return;
        }

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

        for (int i = 0; i < cards.Length; i++)
        {
            UIPlantCardNC[] pageCards =
                cards[i];

            if (pageCards == null)
                continue;

            for (int j = 0; j < pageCards.Length; j++)
            {
                UIPlantCardNC card =
                    pageCards[j];

                if (card.CardPlantType ==
                    PlantType.Nope)
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