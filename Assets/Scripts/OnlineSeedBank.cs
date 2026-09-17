using System.Collections.Generic;
using UnityEngine;

public class OnlineSeedBank : MonoBehaviour
{
    public int CardNum = 4;

    private int DecidedCardNum;
    private int choosedNum;

    public bool isFull;
    public bool isCanClick;

    public Transform group;
    public PlayerShow OwnerShow;

    private RectTransform seedBank;

    private List<UIPlantCard> slotList =
        new List<UIPlantCard>();

    private Dictionary<int, UIPlantCard> localCardToOnlineSlot =
        new Dictionary<int, UIPlantCard>();

    private bool isInitialized;

    public int ChoosedNum
    {
        get
        {
            return choosedNum;
        }
        set
        {
            choosedNum = Mathf.Max(0, value);

            if (choosedNum >= CardNum)
                isFull = true;
            else
                isFull = false;
        }
    }

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (isInitialized)
            return;

        seedBank =
            transform as RectTransform;

        isFull = false;
        isCanClick = true;

        isInitialized = true;
    }

    private bool EnsureInitialized()
    {
        if (!isInitialized ||
            seedBank == null)
        {
            seedBank =
                transform as RectTransform;

            if (seedBank == null)
            {
                Debug.LogError(
                    "OnlineSeedBank: " +
                    gameObject.name +
                    " no tiene RectTransform."
                );

                return false;
            }

            isFull = false;
            isCanClick = true;

            isInitialized = true;
        }

        return true;
    }

    public UIPlantCard GetPreCard(
        UIPlantCard pC)
    {
        int num =
            slotList.IndexOf(pC);

        if (num <= 0)
            return null;

        return slotList[num - 1];
    }

    public void UpdateCD(
        int cardID,
        bool isClear)
    {
        for (int i = 0;
             i < slotList.Count;
             i++)
        {
            UIPlantCard card =
                slotList[i];

            if (card == null)
                continue;

            if (card.CardId == cardID)
            {
                if (!isClear)
                {
                    card.CDEnter();
                }
                else
                {
                    card.currTimeForCd = 0f;
                }

                break;
            }
        }
    }

    public void UpdateSunNum(
        int sunNum)
    {
    }

    public void SpawnCardSlot(
        int soltNum)
    {
        if (!EnsureInitialized())
            return;

        if (group == null)
        {
            Debug.LogError(
                "OnlineSeedBank: " +
                gameObject.name +
                " no tiene asignado el Group."
            );

            return;
        }

        if (GameManager.Instance == null)
        {
            Debug.LogError(
                "OnlineSeedBank: GameManager.Instance es null."
            );

            return;
        }

        if (GameManager.Instance.GameConf == null)
        {
            Debug.LogError(
                "OnlineSeedBank: GameManager.Instance.GameConf es null."
            );

            return;
        }

        if (GameManager.Instance.GameConf.UICardSlot == null)
        {
            Debug.LogError(
                "OnlineSeedBank: GameManager.Instance.GameConf.UICardSlot es null."
            );

            return;
        }

        int newCardNum =
            Mathf.Max(
                0,
                soltNum
            );

        if (slotList.Count == newCardNum)
        {
            bool validSlots = true;

            for (int i = 0;
                 i < slotList.Count;
                 i++)
            {
                if (slotList[i] == null)
                {
                    validSlots = false;
                    break;
                }
            }

            if (validSlots)
            {
                CardNum = newCardNum;

                UpdateSeedBankSize();

                return;
            }
        }

        ClearCardSlot();

        CardNum = newCardNum;

        UpdateSeedBankSize();

        for (int i = 0;
             i < CardNum;
             i++)
        {
            GameObject obj =
                Object.Instantiate(
                    GameManager.Instance.GameConf.UICardSlot
                );

            if (obj == null)
            {
                Debug.LogError(
                    "OnlineSeedBank: No se pudo crear UICardSlot."
                );

                continue;
            }

            UIPlantCard component =
                obj.GetComponent<UIPlantCard>();

            if (component == null)
            {
                Debug.LogError(
                    "OnlineSeedBank: UICardSlot no tiene UIPlantCard."
                );

                Object.Destroy(obj);

                continue;
            }

            component.transform.SetParent(
                group,
                false
            );

            component.CardId = i;

            component.ownerSeedBank =
                this;

            slotList.Add(
                component
            );

            component.transform.localScale =
                new Vector3(
                    1f,
                    1f,
                    1f
                );
        }
    }

    private void UpdateSeedBankSize()
    {
        if (seedBank == null)
            return;

        seedBank.sizeDelta =
            new Vector2(
                18f + CardNum * 48f,
                seedBank.sizeDelta.y
            );
    }

    public void ClearCardSlot()
    {
        localCardToOnlineSlot.Clear();

        for (int i = 0;
             i < slotList.Count;
             i++)
        {
            UIPlantCard card =
                slotList[i];

            if (card == null)
                continue;

            if (card.isChoosed)
            {
                if (GameManager.Instance != null &&
                    GameManager.Instance.isServer &&
                    LVManager.Instance != null &&
                    LVManager.Instance.GameIsStart)
                {
                    if (SeedBank.Instance != null)
                    {
                        SeedBank.Instance.AddCard(
                            card.CardPlantType,
                            card.CardZombieType,
                            card.currTimeForCd,
                            CanUnChoose: true,
                            canAddSlot: true
                        );
                    }
                }

                card.ClearChoose(
                    noAnimation: false
                );

                ChoosedNum--;
            }

            card.DestroyCardSlot();
        }

        ChoosedNum = 0;

        slotList.Clear();
    }

    public void ChooseCard(
        UIPlantCardNC nC,
        bool needAnim)
    {
        ChooseCard(
            nC,
            needAnim,
            -1
        );
    }

    public void ChooseCard(
        UIPlantCardNC nC,
        bool needAnim,
        int localCardId)
    {
        if (nC == null)
            return;

        if (slotList.Count == 0)
            return;

        DecidedCardNum = -1;

        if (localCardId >= 0)
        {
            UIPlantCard mappedCard = null;

            if (localCardToOnlineSlot.TryGetValue(
                    localCardId,
                    out mappedCard))
            {
                if (mappedCard != null &&
                    mappedCard.isChoosed)
                {
                    return;
                }

                localCardToOnlineSlot.Remove(
                    localCardId
                );
            }
        }

        for (int i = 0;
             i < slotList.Count;
             i++)
        {
            UIPlantCard card =
                slotList[i];

            if (card != null &&
                !card.isChoosed)
            {
                DecidedCardNum = i;
                break;
            }
        }

        if (DecidedCardNum < 0 ||
            DecidedCardNum >= slotList.Count)
        {
            return;
        }

        UIPlantCard selectedCard =
            slotList[DecidedCardNum];

        if (selectedCard == null)
            return;

        selectedCard.isChoosed = true;

        ChoosedNum++;

        if (localCardId >= 0)
        {
            localCardToOnlineSlot[
                localCardId
            ] = selectedCard;
        }

        if (needAnim)
        {
            if (PoolManager.Instance == null ||
                GameManager.Instance == null ||
                GameManager.Instance.GameConf == null ||
                GameManager.Instance.GameConf.CardSlotAnimation == null ||
                UIManager.Instance == null)
            {
                selectedCard.AddChoose(nC);

                nC.IsChoosed = true;

                return;
            }

            UIPlantCardAnimation component =
                PoolManager.Instance.GetObj(
                    GameManager.Instance.GameConf.CardSlotAnimation
                ).GetComponent<UIPlantCardAnimation>();

            if (component != null)
            {
                component.transform.SetParent(
                    UIManager.Instance.transform
                );

                if (OwnerShow != null)
                {
                    component.CreateInit(
                        nC.transform.position,
                        nC,
                        null
                    );

                    component.PlayChooseAnimation(
                        OwnerShow.transform.position
                    );
                }
            }
        }

        selectedCard.AddChoose(
            nC
        );

        nC.IsChoosed = true;
    }

    public void ClearChoose(
        int cardId,
        bool needAnim)
    {
        UIPlantCard uIPlantCard = null;

        if (!localCardToOnlineSlot.TryGetValue(
                cardId,
                out uIPlantCard))
        {
            return;
        }

        if (uIPlantCard == null)
        {
            localCardToOnlineSlot.Remove(
                cardId
            );

            return;
        }

        UIPlantCardNC sourceCard =
            uIPlantCard.myPlantCard;

        localCardToOnlineSlot.Remove(
            cardId
        );

        if (uIPlantCard.isChoosed)
        {
            uIPlantCard.isChoosed = false;

            if (ChoosedNum > 0)
                ChoosedNum--;
        }

        uIPlantCard.ClearChoose();

        if (sourceCard == null)
            return;

        sourceCard.IsChoosed = false;

        if (!needAnim)
            return;

        if (PoolManager.Instance == null ||
            GameManager.Instance == null ||
            GameManager.Instance.GameConf == null ||
            GameManager.Instance.GameConf.CardSlotAnimation == null ||
            UIManager.Instance == null)
        {
            return;
        }

        UIPlantCardAnimation component =
            PoolManager.Instance.GetObj(
                GameManager.Instance.GameConf.CardSlotAnimation
            ).GetComponent<UIPlantCardAnimation>();

        if (component == null)
            return;

        component.transform.SetParent(
            UIManager.Instance.transform
        );

        component.CreateInit(
            uIPlantCard.transform.position,
            sourceCard,
            null
        );

        component.PlayChooseAnimation(
            sourceCard.transform.position
        );
    }

    public bool ClearCD(
        PlantType type)
    {
        for (int i = 0;
             i < slotList.Count;
             i++)
        {
            UIPlantCard card =
                slotList[i];

            if (card == null)
                continue;

            if (card.CardPlantType == type &&
                card.currTimeForCd > 0f)
            {
                card.currTimeForCd = 0f;
                return true;
            }
        }

        return false;
    }
}