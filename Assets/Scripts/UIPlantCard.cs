using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UIPlantCard : MonoBehaviour
{
    [Header("Card")]
    public int CardId;
    public int NeedSun;
    public bool isNeedSun;
    public float CDTime;
    public PlantType CardPlantType;
    public ZombieType CardZombieType;
    public float currTimeForCd;
    public bool isChoosed;
    public UIPlantCardNC myPlantCard;
    public bool isImitater;
    public OnlineSeedBank ownerSeedBank;

    [Header("UI")]
    [SerializeField] private Image maskImg;
    [SerializeField] private Image image;
    [SerializeField] private Text WantSunText;

    private Sprite sprite;

    private bool canPlace;
    private bool wantPlace;

    private PlantBase plant;
    private CardState cardState;

    private int currOrderNum = 3;
    private Coroutine CDCoroutine;

    public CardState CardState
    {
        get => cardState;

        set
        {
            if (image == null || maskImg == null || cardState == value)
                return;

            switch (value)
            {
                case CardState.CanPlace:
                    maskImg.fillAmount = 0f;
                    image.color = Color.white;
                    break;

                case CardState.NotCD:
                    image.color = new Color(0.75f, 0.75f, 0.75f);
                    break;

                case CardState.NotSun:
                    maskImg.fillAmount = 0f;
                    image.color = new Color(0.75f, 0.75f, 0.75f);
                    break;

                case CardState.NotAll:
                    image.color = new Color(0.5f, 0.5f, 0.5f);
                    break;
            }

            cardState = value;
        }
    }

    public int CurrOrderNum
    {
        get => currOrderNum;

        set
        {
            currOrderNum = value;

            if (currOrderNum > 30)
                currOrderNum = 3;
        }
    }

    public bool CanPlace
    {
        get => canPlace;

        set
        {
            canPlace = value;
            CheckState();
        }
    }

    public bool WantPlace
    {
        get => wantPlace;

        set
        {
            wantPlace = value;

            if (wantPlace)
            {
                if (PlantManager.Instance == null)
                    return;

                plant = PlantManager.Instance.GetNewPlant(CardPlantType);

                if (plant != null)
                    plant.transform.SetParent(
                        PlantManager.Instance.transform
                    );
            }
            else if (plant != null)
            {
                plant.Dead();
                plant = null;
            }
        }
    }

    private void Awake()
    {
        if (image == null)
            image = GetComponent<Image>();

        if (maskImg == null)
            maskImg = transform.Find("mask")?.GetComponent<Image>();

        if (WantSunText == null)
        {
            Transform textTransform =
                transform.Find("CardSunNumText");

            if (textTransform != null)
                WantSunText =
                    textTransform.GetComponent<Text>();
        }

        if (image != null)
            sprite = image.sprite;
    }

    private void Start()
    {
        InitThis();
    }

    private void InitThis()
    {
        if (image == null || maskImg == null)
            return;

        if (maskImg.material != null)
            maskImg.material =
                Instantiate(maskImg.material);

        if (image.material != null)
            image.material =
                Instantiate(image.material);

        sprite = image.sprite;
        isChoosed = false;

        if (PlayerManager.Instance != null)
            PlayerManager.Instance.AddSunNumUpdateActionListener(
                CheckState
            );

        if (LVManager.Instance != null)
            LVManager.Instance.AddLVStartActionListenr(
                OnLVStartAction
            );

        CanPlace = true;
    }

    private void OnLVStartAction()
    {
        if (image == null)
            return;

        if (CDTime > 7.5f)
        {
            CanPlace = false;
            CDEnter();
        }
        else
        {
            CanPlace = true;
        }
    }

    private void CheckState()
    {
        if (image == null ||
            PlayerManager.Instance == null ||
            ownerSeedBank == null ||
            ownerSeedBank.OwnerShow == null ||
            ownerSeedBank.OwnerShow.nameText == null)
        {
            return;
        }

        string playerName =
            ownerSeedBank.OwnerShow.nameText.name;

        float sunNum =
            PlayerManager.Instance.GetSunNum(
                isNeedSun,
                playerName
            );

        if (canPlace)
        {
            CardState =
                sunNum >= NeedSun
                    ? CardState.CanPlace
                    : CardState.NotSun;
        }
        else
        {
            CardState =
                sunNum >= NeedSun
                    ? CardState.NotCD
                    : CardState.NotAll;
        }
    }

    public void CDEnter()
    {
        if (maskImg == null)
            return;

        if (CDCoroutine != null)
            StopCoroutine(CDCoroutine);

        CanPlace = false;
        maskImg.fillAmount = 1f;

        CDCoroutine = StartCoroutine(CalCD());
    }

    private IEnumerator CalCD()
    {
        if (CDTime <= 0f)
        {
            CanPlace = true;
            CDCoroutine = null;
            yield break;
        }

        float calCD =
            1f / CDTime * 0.1f;

        for (
            currTimeForCd = CDTime;
            currTimeForCd >= 0f;
            currTimeForCd -= 0.1f
        )
        {
            yield return new WaitForSeconds(0.1f);

            if (maskImg != null)
                maskImg.fillAmount -= calCD;
        }

        CanPlace = true;
        CDCoroutine = null;
    }

    public void AddChoose(UIPlantCardNC nC)
    {
        if (nC == null)
            return;

        InitThis();

        isImitater = false;

        if (maskImg != null && maskImg.material != null)
            maskImg.material.SetInt("_OpenGray", 0);

        if (image != null && image.material != null)
            image.material.SetInt("_OpenGray", 0);

        myPlantCard = nC;
        isChoosed = true;

        if (myPlantCard.CardPlantType == PlantType.Imitater)
        {
            isImitater = true;

            if (image != null && image.material != null)
                image.material.SetInt("_OpenGray", 1);

            if (maskImg != null && maskImg.material != null)
                maskImg.material.SetInt("_OpenGray", 1);

            UIPlantCard preCard =
                ownerSeedBank != null
                    ? ownerSeedBank.GetPreCard(this)
                    : null;

            if (preCard == null)
            {
                if (SeedChooser.Instance != null)
                    myPlantCard =
                        SeedChooser.Instance.GetCardInfo(1);
            }
            else if (SeedBank.Instance != null)
            {
                myPlantCard =
                    SeedBank.Instance.GetPlantNc(
                        preCard.CardPlantType
                    );
            }
        }

        if (myPlantCard == null)
            return;

        NeedSun = myPlantCard.NeedNum;
        isNeedSun = myPlantCard.isNeedSun;
        CDTime = myPlantCard.CDTime;
        CardPlantType = myPlantCard.CardPlantType;
        CardZombieType = myPlantCard.CardZombieType;

        if (image != null)
            image.sprite = myPlantCard.OwnerSprite;

        if (maskImg != null)
            maskImg.sprite = myPlantCard.OwnerSprite;

        if (WantSunText != null)
            WantSunText.text = NeedSun.ToString();

        if (isImitater &&
            SeedBank.Instance != null)
        {
            myPlantCard =
                SeedBank.Instance.GetPlantNc(
                    PlantType.Imitater
                );
        }

        if (LV.Instance != null &&
            LV.Instance.LvSpStates.Contains(
                LVSpState.FreeDay
            ))
        {
            NeedSun = 0;

            if (WantSunText != null)
                WantSunText.text = "0";
        }
    }

    public void ClearChoose()
    {
        if (image != null)
            image.sprite = sprite;

        if (maskImg != null)
            maskImg.sprite = sprite;

        if (WantSunText != null)
            WantSunText.text = "";

        isChoosed = false;
        NeedSun = 0;
        CDTime = 0f;
        CardPlantType = PlantType.Nope;
    }

    public void ClearChoose(bool noAnimation)
    {
        if (myPlantCard != null)
            myPlantCard.IsChoosed = false;
    }

    public void ClearChooseOk()
    {
    }

    public void DestroyCardSlot()
    {
        Destroy(gameObject);
    }
}