using SocketSave;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIPlantCardNC : MonoBehaviour, IPointerDownHandler, IEventSystemHandler
{
    [Header("UI")]
    [SerializeField] private Image cardImage;
    [SerializeField] private Text needText;

    public Sprite OwnerSprite;

    private bool isChoosed;
    private bool isUnLock = true;

    public bool isNeedSun;
    public int NeedNum;
    public float CDTime;

    public PlantType CardPlantType;
    public ZombieType CardZombieType;

    public bool isForCreate;

    public bool IsChoosed
    {
        get => isChoosed;

        set
        {
            if (!isUnLock)
                return;

            isChoosed = value;

            if (cardImage != null)
            {
                cardImage.color = value
                    ? new Color(0.3f, 0.3f, 0.3f)
                    : Color.white;
            }
        }
    }

    public bool IsUnLock
    {
        get => isUnLock;

        set
        {
            isUnLock = value;

            if (cardImage == null)
                return;

            if (value)
            {
                cardImage.sprite = OwnerSprite;

                if (needText != null)
                {
                    needText.text = NeedNum.ToString();

                    if (LV.Instance != null &&
                        LV.Instance.LvSpStates.Contains(LVSpState.FreeDay))
                    {
                        needText.text = "0";
                    }
                }
            }
            else
            {
                if (SeedBank.Instance != null)
                    cardImage.sprite = SeedBank.Instance.NoCardSprite;

                if (needText != null)
                    needText.text = "";
            }
        }
    }

    private void Awake()
    {
        if (cardImage == null)
            cardImage = GetComponent<Image>();

        if (needText == null)
            needText = GetComponentInChildren<Text>(true);

        if (cardImage != null)
            OwnerSprite = cardImage.sprite;
    }

    private void Start()
    {
        IsChoosed = false;
    }

    public void ClickThis(bool haveSound)
    {
        if (!IsUnLock)
            return;

        if (CardPlantType == PlantType.Heronsbill &&
            LV.Instance != null &&
            LV.Instance.CurrLVType == LVType.PvP)
        {
            if (haveSound &&
                AudioManager.Instance != null &&
                GameManager.Instance != null &&
                GameManager.Instance.AudioConf != null)
            {
                AudioManager.Instance.PlayEFAudio(
                    GameManager.Instance.AudioConf.Buzzer,
                    transform.position,
                    isAll: true
                );
            }

            return;
        }

        if (isForCreate)
        {
            if (SeedChooser.Instance != null)
                SeedChooser.Instance.ChooseOver();

            if (ZombieChooser.Instance != null)
                ZombieChooser.Instance.ChooseOver();

            if (CreatePanel.Instance != null)
            {
                CreatePanel.Instance.SelectCard(
                    CardPlantType,
                    CardZombieType
                );
            }

            return;
        }

        if (!IsChoosed &&
            (CardPlantType != PlantType.Nope ||
             CardZombieType != ZombieType.Nope) &&
            SeedBank.Instance != null &&
            !SeedBank.Instance.isFull &&
            SeedChooser.Instance != null &&
            !SeedChooser.Instance.isPrepare)
        {
            if (!SeedBank.Instance.ChooseCard(this))
                return;

            IsChoosed = true;

            if (haveSound &&
                AudioManager.Instance != null &&
                GameManager.Instance != null &&
                GameManager.Instance.AudioConf != null)
            {
                AudioClip tapAudio =
                    Random.Range(0, 2) == 1
                        ? GameManager.Instance.AudioConf.Tap
                        : GameManager.Instance.AudioConf.Tap2;

                AudioManager.Instance.PlayEFAudio(
                    tapAudio,
                    transform.position,
                    isAll: true
                );
            }

            if (GameManager.Instance == null ||
                GameManager.Instance.LocalPlayerSave == null)
            {
                return;
            }

            string playerName =
                GameManager.Instance.LocalPlayerSave.playerName;

            if (string.IsNullOrEmpty(playerName))
                return;

            int localCardId =
                SeedBank.Instance.LastChosenCardId;

            if (BattlePlayerList.Instance != null)
            {
                BattlePlayerList.Instance.SelectCard(
                    playerName,
                    CardPlantType,
                    CardZombieType,
                    false,
                    localCardId
                );
            }

            if (GameManager.Instance.isClient &&
                OnlineNetworkClient.Instance != null)
            {
                SelectCard selectCard = new SelectCard
                {
                    plantType = CardPlantType,
                    zombieType = CardZombieType,
                    isBack = false,
                    cardId = localCardId
                };

                OnlineNetworkClient.Instance.SelectCard(
                    selectCard
                );
            }

            return;
        }

        if (haveSound &&
            AudioManager.Instance != null &&
            GameManager.Instance != null &&
            GameManager.Instance.AudioConf != null)
        {
            AudioManager.Instance.PlayEFAudio(
                GameManager.Instance.AudioConf.Buzzer,
                transform.position,
                isAll: true
            );
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        ClickThis(true);
    }
}