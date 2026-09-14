using SaveClass;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LevelDisPlay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public Image MapImage;
    public Text LevelText;
    public Image REnderer;
    public Image LvCard;
    public Transform LockImg;

    public LvSave lVInfo;

    private LevelSelector levelSelector;
    private LV lv;

    private static readonly Color32 LockedColor = new Color32(100, 100, 100, 255);
    private static readonly Color32 SelectedColor = new Color32(150, 150, 150, 255);
    private static readonly Color32 WhiteColor = new Color32(255, 255, 255, 255);

    private void Awake()
    {
        levelSelector = LevelSelector.Instance;
        lv = LV.Instance;
    }

    public void OpenInit(LvSave info, LvSave lastInfo)
    {
        lVInfo = info;

        if (LockImg != null)
            LockImg.localScale = Vector3.one;

        if (LvCard != null)
            LvCard.color = LockedColor;

        if (MapImage != null)
            MapImage.color = LockedColor;

        if (info == null)
        {
            if (LevelText != null)
                LevelText.text = "暂无关卡";

            if (LvCard != null)
                LvCard.transform.localScale = Vector3.zero;

            if (MapImage != null && NormalSprite.Instance != null)
                MapImage.sprite = NormalSprite.Instance.YardDay;

            return;
        }

        if (lv == null)
            lv = LV.Instance;

        if (levelSelector == null)
            levelSelector = LevelSelector.Instance;

        if (lv == null || levelSelector == null)
            return;

        lv.LoadLV(
            info.LvId,
            levelSelector.IsEasy,
            onlyInfo: true,
            isRun: false
        );

        if (MapImage != null)
        {
            MapImage.sprite = lv.GetLvSprite();

            int seriesId = info.LvId / 10000;
            int categoryId = info.LvId % 10000 / 1000;
            int levelId = info.LvId % 1000 - 1;

            if (categoryId == 1 &&
                seriesId == 1 &&
                NormalSprite.Instance != null &&
                levelId >= 0 &&
                levelId < NormalSprite.Instance.YardMiniGame.Count)
            {
                MapImage.sprite =
                    NormalSprite.Instance.YardMiniGame[levelId];
            }
        }

        if (lv.BootyPlant == PlantType.Nope)
        {
            if (LvCard != null)
                LvCard.transform.localScale = Vector3.zero;
        }
        else
        {
            if (LvCard != null && SeedChooser.Instance != null)
            {
                var cardInfo =
                    SeedChooser.Instance.GetCardInfo(lv.BootyPlant);

                if (cardInfo != null)
                {
                    LvCard.sprite = cardInfo.OwnerSprite;
                    LvCard.transform.localScale = Vector3.one;
                }
                else
                {
                    LvCard.transform.localScale = Vector3.zero;
                }
            }
        }

        if (LevelText != null)
            LevelText.text = lv.LvName;

        if (levelSelector.SelectedLvSave != null &&
            levelSelector.SelectedLvSave.LvId == lVInfo.LvId)
        {
            if (REnderer != null)
                REnderer.color = SelectedColor;
        }
        else if (REnderer != null)
        {
            REnderer.color = WhiteColor;
        }

        if (lastInfo == null ||
            lastInfo.PassNum > 0 ||
            lastInfo.HardPNum > 0)
        {
            if (LockImg != null)
                LockImg.localScale = Vector3.zero;

            if (MapImage != null)
                MapImage.color = WhiteColor;

            if (LvCard != null)
                LvCard.color = WhiteColor;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (lVInfo == null)
            return;

        if (levelSelector == null)
            levelSelector = LevelSelector.Instance;

        if (levelSelector == null)
            return;

        if (LockImg != null &&
            LockImg.localScale.x > 0f &&
            levelSelector.transform.localScale.x > 0f)
        {
            if (GameManager.Instance != null &&
                GameManager.Instance.AudioConf != null)
            {
                PlayAudio(GameManager.Instance.AudioConf.Buzzer);
            }

            return;
        }

        if (levelSelector.SelectedLvSave == null ||
            levelSelector.SelectedLvSave.LvId != lVInfo.LvId)
        {
            if (eventData != null &&
                GameManager.Instance != null &&
                GameManager.Instance.AudioConf != null)
            {
                PlayAudio(GameManager.Instance.AudioConf.ButtonClick);
            }

            levelSelector.SelectThis(this);

            if (REnderer != null)
                REnderer.color = SelectedColor;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (lVInfo != null &&
            LockImg != null &&
            LockImg.localScale.x <= 0f)
        {
            if (REnderer != null)
                REnderer.color = SelectedColor;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (lVInfo != null &&
            levelSelector != null &&
            (levelSelector.SelectedLvSave == null ||
             levelSelector.SelectedLvSave.LvId != lVInfo.LvId))
        {
            if (REnderer != null)
                REnderer.color = WhiteColor;
        }
    }

    private void PlayAudio(AudioClip clip)
    {
        if (clip == null || AudioManager.Instance == null)
            return;

        AudioManager.Instance.PlayEFAudio(
            clip,
            transform.position,
            isAll: true
        );
    }
}