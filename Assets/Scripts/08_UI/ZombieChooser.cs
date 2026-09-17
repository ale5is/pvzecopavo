using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ZombieChooser : MonoBehaviour
{
    public static ZombieChooser Instance;

    public StartButton startButton;

    public List<Transform> Pages = new List<Transform>();

    private int currPage;

    public UIPlantCardNC ForGetSizeNc;

    public Transform ChangeChooserBtn;

    public Transform CallBtn;

    public Transform LookGroundBtn;

    public bool isPrepare;

    private RectTransform rectTransform;

    public int CurrPage
    {
        get
        {
            return currPage;
        }
        set
        {
            if (Pages == null || Pages.Count == 0)
            {
                currPage = 0;
                return;
            }

            int index = currPage;

            if (value > Pages.Count - 1)
            {
                currPage = 0;
            }
            else if (value < 0)
            {
                currPage = Pages.Count - 1;
            }
            else
            {
                currPage = value;
            }

            if (index >= 0 && index < Pages.Count && Pages[index] != null)
            {
                Pages[index].gameObject.SetActive(false);
            }

            if (currPage >= 0 &&
                currPage < Pages.Count &&
                Pages[currPage] != null)
            {
                Pages[currPage].gameObject.SetActive(true);
            }
        }
    }

    private void Awake()
    {
        Instance = this;

        rectTransform = GetComponent<RectTransform>();
    }

    private void Start()
    {
        if (Pages == null || Pages.Count == 0)
        {
            return;
        }

        for (int i = 0; i < Pages.Count; i++)
        {
            if (Pages[i] != null)
            {
                Pages[i].gameObject.SetActive(false);
            }
        }

        CurrPage = 0;
    }

    public UIPlantCardNC[] GetCardInfos(int pageIndex)
    {
        if (Pages == null ||
            pageIndex < 0 ||
            pageIndex >= Pages.Count ||
            Pages[pageIndex] == null)
        {
            return new UIPlantCardNC[0];
        }

        return Pages[pageIndex].GetComponentsInChildren<UIPlantCardNC>(true);
    }

    public UIPlantCardNC GetCardInfo(PlantType plantType)
    {
        if (plantType == PlantType.Nope)
        {
            return null;
        }

        for (int i = 0; i < Pages.Count; i++)
        {
            if (Pages[i] == null)
            {
                continue;
            }

            UIPlantCardNC[] componentsInChildren =
                Pages[i].GetComponentsInChildren<UIPlantCardNC>(true);

            for (int j = 0; j < componentsInChildren.Length; j++)
            {
                if (componentsInChildren[j].CardPlantType == plantType)
                {
                    return componentsInChildren[j];
                }
            }
        }

        return null;
    }

    public UIPlantCardNC GetCardInfo(ZombieType zombieType)
    {
        if (zombieType == ZombieType.Nope)
        {
            return null;
        }

        for (int i = 0; i < Pages.Count; i++)
        {
            if (Pages[i] == null)
            {
                continue;
            }

            UIPlantCardNC[] componentsInChildren =
                Pages[i].GetComponentsInChildren<UIPlantCardNC>(true);

            for (int j = 0; j < componentsInChildren.Length; j++)
            {
                if (componentsInChildren[j].CardZombieType == zombieType)
                {
                    return componentsInChildren[j];
                }
            }
        }

        return null;
    }

    public UIPlantCardNC GetCardInfo(int cardId)
    {
        int num = 0;

        for (int i = 0; i < Pages.Count; i++)
        {
            if (Pages[i] == null)
            {
                continue;
            }

            UIPlantCardNC[] componentsInChildren =
                Pages[i].GetComponentsInChildren<UIPlantCardNC>(true);

            for (int j = 0; j < componentsInChildren.Length; j++)
            {
                num++;

                if (num == cardId)
                {
                    return componentsInChildren[j];
                }
            }
        }

        return null;
    }

    public PlantType GetCardType(int cardId)
    {
        int num = 0;

        for (int i = 0; i < Pages.Count; i++)
        {
            if (Pages[i] == null)
            {
                continue;
            }

            UIPlantCardNC[] componentsInChildren =
                Pages[i].GetComponentsInChildren<UIPlantCardNC>(true);

            for (int j = 0; j < componentsInChildren.Length; j++)
            {
                num++;

                if (num == cardId)
                {
                    return componentsInChildren[j].CardPlantType;
                }
            }
        }

        return PlantType.Nope;
    }

    public void ClearAllChoose()
    {
        for (int i = 0; i < Pages.Count; i++)
        {
            if (Pages[i] == null)
            {
                continue;
            }

            UIPlantCardNC[] componentsInChildren =
                Pages[i].GetComponentsInChildren<UIPlantCardNC>(true);

            for (int j = 0; j < componentsInChildren.Length; j++)
            {
                componentsInChildren[j].IsChoosed = false;
            }
        }
    }

    public void ResetThis()
    {
        StopAllCoroutines();

        if (Pages != null && Pages.Count > 0)
        {
            CurrPage = 0;
        }

        if (startButton != null)
        {
            startButton.gameObject.SetActive(true);
        }

        if (CallBtn != null)
        {
            CallBtn.gameObject.SetActive(true);
        }

        if (LookGroundBtn != null)
        {
            LookGroundBtn.gameObject.SetActive(true);
        }

        if (rectTransform != null)
        {
            rectTransform.anchoredPosition =
                new Vector3(-600f, -45f, 0f);
        }

        for (int i = 0; i < Pages.Count; i++)
        {
            if (Pages[i] == null)
            {
                continue;
            }

            UIPlantCardNC[] componentsInChildren =
                Pages[i].GetComponentsInChildren<UIPlantCardNC>(true);

            for (int j = 0; j < componentsInChildren.Length; j++)
            {
                if (componentsInChildren[j].CardZombieType != ZombieType.Nope ||
                    componentsInChildren[j].CardPlantType != PlantType.Nope)
                {
                    componentsInChildren[j].isForCreate = false;
                }
            }
        }
    }

    public void StartMove()
    {
        // El objeto debe estar activo antes de iniciar la coroutine.
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (GameManager.Instance.isClient)
        {
            if (SpectatorList.Instance.LocalIsSpectator)
            {
                isPrepare = true;

                if (startButton != null)
                {
                    startButton.StartText.text = "旁观中";
                }
            }
            else
            {
                isPrepare = false;

                if (startButton != null)
                {
                    if (isPrepare)
                    {
                        startButton.StartText.text = "取消准备";
                    }
                    else
                    {
                        startButton.StartText.text = "准备";
                    }
                }
            }
        }
        else
        {
            if (SpectatorList.Instance.LocalIsSpectator)
            {
                isPrepare = true;
            }
            else
            {
                isPrepare = false;
            }

            if (startButton != null)
            {
                startButton.StartText.text = "开始战斗";
            }
        }

        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        if (Camera.main == null ||
            SeedBank.Instance == null)
        {
            return;
        }

        StartCoroutine(
            DoMove(
                (
                    (Vector2)Camera.main.WorldToScreenPoint(
                        SeedBank.Instance.transform.position +
                        new Vector3(5f, 0f)
                    )
                ).x,
                false
            )
        );
    }

    public void StartRunLv(bool synClient = false)
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (!GameManager.Instance.isClient || synClient)
        {
            StartCoroutine(DoMove(-800f, true));
        }
    }

    public void Prepare()
    {
        isPrepare = !isPrepare;

        if (startButton == null)
        {
            return;
        }

        if (isPrepare)
        {
            startButton.StartText.text = "取消准备";
        }
        else
        {
            startButton.StartText.text = "准备";
        }
    }

    private IEnumerator DoMove(float targetPosX, bool isOut)
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        Vector3 vector =
            new Vector3(
                targetPosX,
                transform.position.y,
                transform.position.z
            );

        Vector2 dir =
            (vector - transform.position).normalized *
            Time.deltaTime *
            4000f;

        if (targetPosX > transform.position.x)
        {
            while (targetPosX > transform.position.x)
            {
                yield return null;

                rectTransform.Translate(dir);
            }
        }
        else
        {
            while (targetPosX < transform.position.x)
            {
                yield return null;

                rectTransform.Translate(dir);
            }
        }

        if (isOut)
        {
            ClearAllChoose();

            gameObject.SetActive(false);
        }
    }

    public void OpenForCreate()
    {
        if (!LVManager.Instance.GameIsStart)
        {
            return;
        }

        gameObject.SetActive(true);

        if (startButton != null)
        {
            startButton.gameObject.SetActive(false);
        }

        if (CallBtn != null)
        {
            CallBtn.gameObject.SetActive(false);
        }

        if (LookGroundBtn != null)
        {
            LookGroundBtn.gameObject.SetActive(false);
        }

        if (Camera.main != null &&
            CameraControl.Instance != null &&
            rectTransform != null)
        {
            Vector2 vector =
                Camera.main.WorldToScreenPoint(
                    CameraControl.Instance.transform.position
                );

            rectTransform.position = vector;
        }

        if (ChangeChooserBtn != null)
        {
            ChangeChooserBtn.gameObject.SetActive(false);
        }

        for (int i = 0; i < Pages.Count; i++)
        {
            if (Pages[i] == null)
            {
                continue;
            }

            UIPlantCardNC[] componentsInChildren =
                Pages[i].GetComponentsInChildren<UIPlantCardNC>(true);

            for (int j = 0; j < componentsInChildren.Length; j++)
            {
                if (componentsInChildren[j].CardZombieType != ZombieType.Nope ||
                    componentsInChildren[j].CardPlantType != PlantType.Nope)
                {
                    componentsInChildren[j].IsUnLock = true;
                    componentsInChildren[j].isForCreate = true;
                }
            }
        }
    }

    public void ChooseOver()
    {
        StopAllCoroutines();

        ClearAllChoose();

        gameObject.SetActive(false);

        ResetThis();
    }
}