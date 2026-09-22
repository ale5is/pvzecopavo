using System.Collections.Generic;
using SocketSave;
using UnityEngine;
using UnityEngine.UI;

public class FlagMeter : MonoBehaviour
{
    public static FlagMeter Instance;

    // =========================================================
    // REFERENCIAS - INSPECTOR
    // =========================================================

    [Header("Managers")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private LVManager lvManager;
    [SerializeField] private LV lv;
    [SerializeField] private OnlineNetworkServer socketServer;

    [Header("Prefabs")]
    [SerializeField] private LVFlag lvFlagPrefab;

    [Header("UI")]
    [SerializeField] private Image Head;
    [SerializeField] private Image MaskImg;

    [SerializeField] public RectTransform SizeMark1;
    [SerializeField] public RectTransform SizeMark2;
    [SerializeField] public RectTransform SizeMark3;

    [SerializeField] public Text name1;
    [SerializeField] public Text name2;
    [SerializeField] public Text name3;

    [SerializeField] public Text LvSpText1;
    [SerializeField] public Text LvSpText2;
    [SerializeField] public Text LvSpText3;

    [SerializeField] public Text NextSubLvText;

    // =========================================================
    // DATOS
    // =========================================================

    private int Allweight;

    private readonly List<LVFlag> FlagList =
        new List<LVFlag>();

    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (Head != null && SizeMark2 != null)
        {
            Head.rectTransform.localPosition =
                SizeMark2.localPosition;
        }

        if (MaskImg != null)
        {
            MaskImg.fillAmount = 0f;
        }
    }

    // =========================================================
    // NIVEL
    // =========================================================

    public void SetLvlName(string name, bool isEasy)
    {
        if (name2 != null)
        {
            name2.color = isEasy
                ? new Color32(255, 236, 0, 255)
                : new Color32(255, 74, 0, 255);
        }

        if (name1 != null)
        {
            name1.text = name;
        }

        if (name2 != null)
        {
            name2.text = name;
        }

        if (name3 != null)
        {
            name3.text = name;
        }

        if (LvSpText1 != null)
        {
            LvSpText1.text = "";
        }

        if (LvSpText2 != null)
        {
            LvSpText2.text = "";
        }

        if (LvSpText3 != null)
        {
            LvSpText3.text = "";
        }
    }

    private void ResetFlagMeter()
    {
        SetRestTimeText(null);

        if (Head != null)
        {
            Head.transform.localScale = Vector3.one;

            if (SizeMark2 != null)
            {
                Head.transform.localPosition =
                    SizeMark2.localPosition;
            }
        }

        if (MaskImg != null)
        {
            MaskImg.fillAmount = 0f;
        }

        for (int i = 0; i < FlagList.Count; i++)
        {
            if (FlagList[i] != null)
            {
                FlagList[i].Destroy();
            }
        }

        FlagList.Clear();
    }

    // =========================================================
    // CABEZAL
    // =========================================================

    public void UpdateHead(int weight)
    {
        if (
            gameManager == null ||
            lvManager == null ||
            Head == null ||
            MaskImg == null ||
            Allweight <= 0
        )
        {
            return;
        }

        if (
            gameManager.isClient ||
            lvManager.isBigWave
        )
        {
            return;
        }

        float distance =
            Mathf.Abs(
                SizeMark1.transform.position.x -
                SizeMark2.transform.position.x
            ) / Allweight;

        Head.transform.position +=
            new Vector3(
                -distance * weight,
                0f,
                0f
            );

        if (
            Head.transform.position.x <
            SizeMark1.transform.position.x
        )
        {
            Head.transform.position =
                new Vector3(
                    SizeMark1.transform.position.x,
                    Head.transform.position.y
                );
        }

        MaskImg.fillAmount +=
            1f / Allweight * weight;

        MaskImg.fillAmount =
            Mathf.Clamp01(
                MaskImg.fillAmount
            );

        if (gameManager.isServer)
        {
            SendFlagMeter(
                1,
                MaskImg.fillAmount,
                null
            );
        }
    }

    // =========================================================
    // I ZOMBIE
    // =========================================================

    public void IZUpdate(int curr, int allNum)
    {
        if (
            gameManager == null ||
            gameManager.isClient ||
            MaskImg == null ||
            allNum <= 0
        )
        {
            return;
        }

        string text =
            $"食脑{curr}/{allNum}";

        if (LvSpText1 != null)
        {
            LvSpText1.text = text;
        }

        if (LvSpText2 != null)
        {
            LvSpText2.text = text;
        }

        if (LvSpText3 != null)
        {
            LvSpText3.text = text;
        }

        MaskImg.fillAmount =
            Mathf.Clamp01(
                (float)curr / allNum
            );

        if (gameManager.isServer)
        {
            SendFlagMeter(
                1,
                MaskImg.fillAmount,
                text
            );
        }
    }

    // =========================================================
    // TEXTO RESTANTE
    // =========================================================

    public void SetRestTimeText(string text)
    {
        bool empty =
            string.IsNullOrEmpty(text);

        if (NextSubLvText != null)
        {
            Transform parent =
                NextSubLvText.transform.parent;

            if (parent != null)
            {
                parent.localScale =
                    empty
                        ? Vector3.zero
                        : Vector3.one;
            }

            if (!empty)
            {
                NextSubLvText.text = text;
            }
        }

        if (
            gameManager != null &&
            gameManager.isServer
        )
        {
            SendFlagMeter(
                3,
                0f,
                LvSpText1 != null
                    ? LvSpText1.text
                    : ""
            );
        }
    }

    // =========================================================
    // SINCRONIZACION
    // =========================================================

    private void SendFlagMeter(
        int type,
        float fill,
        string text)
    {
        if (socketServer == null)
        {
            return;
        }

        FlagMeterSyn syn =
            new FlagMeterSyn();

        syn.Type = type;
        syn.FillA = fill;
        syn.SpTxt = text;

        socketServer.SynFlagMeter(syn);
    }

    public void ClientSyn(FlagMeterSyn syn)
    {
        if (syn.Type == 1)
        {
            if (MaskImg != null)
            {
                MaskImg.fillAmount =
                    syn.FillA;
            }

            if (LvSpText1 != null)
            {
                LvSpText1.text = syn.SpTxt;
            }

            if (LvSpText2 != null)
            {
                LvSpText2.text = LvSpText1.text;
            }

            if (LvSpText3 != null)
            {
                LvSpText3.text = LvSpText1.text;
            }

            if (
                Head != null &&
                SizeMark1 != null &&
                SizeMark2 != null
            )
            {
                float distance =
                    Mathf.Abs(
                        SizeMark1.transform.position.x -
                        SizeMark2.transform.position.x
                    );

                Head.transform.position =
                    new Vector3(
                        SizeMark2.transform.position.x -
                        distance * syn.FillA,
                        Head.transform.position.y
                    );

                if (
                    Head.transform.position.x <
                    SizeMark1.transform.position.x
                )
                {
                    Head.transform.position =
                        new Vector3(
                            SizeMark1.transform.position.x,
                            Head.transform.position.y
                        );
                }
            }
        }
        else if (syn.Type == 2)
        {
            ResetFlagMeter();
            CreateFlag(syn.FlagPos);
        }
        else if (syn.Type == 3)
        {
            SetRestTimeText(syn.SpTxt);
        }
    }

    // =========================================================
    // CREAR BANDERAS
    // =========================================================

    public void CreateFlag(int allWeight)
    {
        ResetFlagMeter();

        Allweight = allWeight;

        transform.localScale = Vector3.one;

        if (
            name3 != null &&
            SizeMark3 != null
        )
        {
            name3.rectTransform.position =
                SizeMark3.position;
        }

        if (
            lv != null &&
            lv.CurrLVType == LVType.IZombie
        )
        {
            if (Head != null)
            {
                Head.transform.localScale =
                    Vector3.zero;
            }

            return;
        }

        if (Allweight == 0)
        {
            if (name3 != null)
            {
                name3.transform.position =
                    transform.position;
            }

            transform.localScale =
                Vector3.zero;

            return;
        }

        List<float> flagPositions =
            new List<float>();

        if (
            lv == null ||
            lv.Weights == null ||
            lv.Weights.Count == 0
        )
        {
            return;
        }

        int bigWaveIndex = 0;
        float currentWeight = 0f;

        for (
            int i = 0;
            i < lv.Weights[0].Count;
            i++
        )
        {
            if (
                bigWaveIndex < lv.BigWaveNum.Count &&
                i == lv.BigWaveNum[bigWaveIndex]
            )
            {
                flagPositions.Add(
                    currentWeight /
                    Allweight
                );

                bigWaveIndex++;
            }
            else
            {
                for (
                    int j = 0;
                    j < lv.Weights.Count;
                    j++
                )
                {
                    if (i < lv.Weights[j].Count)
                    {
                        currentWeight +=
                            lv.Weights[j][i];
                    }
                }
            }
        }

        CreateFlag(flagPositions);

        if (
            gameManager != null &&
            gameManager.isServer
        )
        {
            FlagMeterSyn syn =
                new FlagMeterSyn();

            syn.Type = 2;
            syn.FlagPos = flagPositions;

            if (socketServer != null)
            {
                socketServer.SynFlagMeter(syn);
            }
        }
    }

    public void CreateFlag(List<float> FlagPos)
    {
        if (
            SizeMark1 == null ||
            SizeMark2 == null ||
            lvFlagPrefab == null
        )
        {
            return;
        }

        float distance =
            Mathf.Abs(
                SizeMark1.transform.position.x -
                SizeMark2.transform.position.x
            );

        for (int i = 0; i < FlagPos.Count; i++)
        {
            LVFlag flag =
                Instantiate(lvFlagPrefab);

            if (flag == null)
            {
                continue;
            }

            FlagList.Add(flag);

            flag.transform.SetParent(
                transform
            );

            flag.CreateInit(
                SizeMark1.rect
            );

            flag.transform.position =
                new Vector3(
                    SizeMark2.transform.position.x -
                    distance * FlagPos[i],
                    transform.position.y,
                    0f
                );

            flag.transform.SetAsLastSibling();

            RectTransform rect =
                flag.transform as RectTransform;

            if (rect != null)
            {
                rect.localScale =
                    Vector3.one;
            }
        }

        if (Head != null)
        {
            Head.transform.SetAsLastSibling();
        }

        if (NextSubLvText != null)
        {
            NextSubLvText.transform
                .parent
                .SetAsLastSibling();
        }
    }

    // =========================================================
    // BANDERA
    // =========================================================

    public void FlagRise(int index)
    {
        if (
            index < 0 ||
            index >= FlagList.Count ||
            FlagList[index] == null ||
            SizeMark2 == null
        )
        {
            return;
        }

        FlagList[index].GoRise(
            SizeMark2.localPosition.x
        );
    }
}