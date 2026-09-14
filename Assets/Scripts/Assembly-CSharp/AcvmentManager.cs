using SaveClass;
using System;
using System.Collections.Generic;
using UnityEngine;

public class AcvmentManager : MonoBehaviour
{
    public static AcvmentManager Instance;

    public GameObject AchivPrefab;
    public Sprite EmptyIcon;
    public List<Sprite> AcvIcons = new List<Sprite>();

    private readonly List<TextMesh> LoadedAcvOverNum = new List<TextMesh>();
    private readonly List<SpriteRenderer> LoadedAcvRederer = new List<SpriteRenderer>();

    private readonly List<Acvname> achievementList = new List<Acvname>
    {
        Acvname.Potato5,
        Acvname.Cherry25,
        Acvname.Popcorn4,
        Acvname.Squash10,
        Acvname.ClickMoney100,
        Acvname.SunFull,
        Acvname.RollNut5,
        Acvname.Plant49,
        Acvname.UnitAsOne,
        Acvname.Sunflower200,
        Acvname.Money100w,
        Acvname.LuckCorn5,
        Acvname.UltimateKill,
        Acvname.CheatSquash50,
        Acvname.ChaosZombie,
        Acvname.FlatSquash,
        Acvname.IceShroom20
    };

    private readonly List<Acvname> CurrLvOverAcv = new List<Acvname>();

    private readonly List<Acvname> GetOnlyOne = new List<Acvname>
    {
        Acvname.Plant49,
        Acvname.Money100w
    };

    private List<int> OverAcvmentNum
    {
        get
        {
            if (GameManager.Instance == null)
                return null;

            if (GameManager.Instance.StatsAcvSave == null)
                GameManager.Instance.StatsAcvSave =
                    new StatsAndAcvSave();

            if (GameManager.Instance.StatsAcvSave.OverAcvNum == null)
                GameManager.Instance.StatsAcvSave.OverAcvNum =
                    new List<int>();

            return GameManager.Instance.StatsAcvSave.OverAcvNum;
        }
    }

    private void Awake()
    {
        Instance = this;
    }

    public void LoadAcvSave()
    {
        List<int> save = OverAcvmentNum;

        if (save == null)
            return;

        int requiredCount =
            Enum.GetValues(typeof(Acvname)).Length;

        if (save.Count >= requiredCount)
            return;

        int missing =
            requiredCount - save.Count;

        for (int i = 0; i < missing; i++)
        {
            save.Add(0);
        }
    }

    public void LVResetThis()
    {
        CurrLvOverAcv.Clear();
    }

    public void GetAchievement(
        Acvname achiv,
        string playerName)
    {
        if (GameManager.Instance == null)
            return;

        if (GameManager.Instance.LocalPlayerSave != null &&
            playerName ==
            GameManager.Instance.LocalPlayerSave.playerName)
        {
            GetAchievement(achiv);
            return;
        }

        if (GameManager.Instance.isServer &&
            SocketServer.Instance != null)
        {
            SocketServer.Instance.SendAcvmentGet(
                achiv,
                playerName
            );
        }
    }

    public void GetAchievement(Acvname achiv)
    {
        if (GameManager.Instance == null)
            return;

        List<int> save = OverAcvmentNum;

        if (save == null)
            return;

        int index = (int)achiv;

        if (index < 0)
            return;

        while (save.Count <= index)
        {
            save.Add(0);
        }

        if (CurrLvOverAcv.Contains(achiv))
            return;

        CurrLvOverAcv.Add(achiv);

        if (save[index] == 0)
        {
            if (StatsManager.Instance != null)
            {
                StatsManager.Instance.AddStatsNum(
                    StatsEnum.OverAcvNum
                );
            }

            if (ChatInput.Instance != null &&
                GameManager.Instance.LocalPlayerSave != null)
            {
                ChatInput.Instance.SendMessageToAll(
                    "Completó el logro <color=#41FF00>[" +
                    GetAchivName(achiv) +
                    "]</color>!",
                    needName: true
                );
            }

            if (AudioManager.Instance != null &&
                GameManager.Instance.AudioConf != null)
            {
                AudioManager.Instance.PlayEFAudio(
                    GameManager.Instance.AudioConf.Achivment,
                    Vector2.zero,
                    isAll: true
                );
            }
        }

        save[index]++;

        if (save[index] == 1)
        {
            GameManager.Instance.SaveSAInfo();
        }

        if (GetOnlyOne.Contains(achiv))
        {
            save[index] = 1;
        }
    }

    public void InitAcvment()
    {
        if (AchivPrefab == null)
            return;

        if (LoadedAcvRederer.Count == 0)
        {
            CreateAchievementUI();
        }

        UpdateAchievementUI();
    }

    private void CreateAchievementUI()
    {
        for (int i = 0; i < achievementList.Count; i++)
        {
            GameObject obj =
                Instantiate(AchivPrefab, transform);

            if (obj == null)
                continue;

            Transform iconTransform =
                obj.transform.Find("AcvIcons");

            Transform titleTransform =
                obj.transform.Find("AcvTitle");

            Transform contentTransform =
                obj.transform.Find("AcvContent");

            Transform overNumTransform =
                obj.transform.Find("AcvOverNum");

            if (iconTransform == null ||
                titleTransform == null ||
                contentTransform == null ||
                overNumTransform == null)
            {
                Destroy(obj);
                continue;
            }

            obj.transform.localPosition =
                new Vector3(
                    0f,
                    -2.5f - 1.5f * i
                );

            SpriteRenderer icon =
                iconTransform.GetComponent<SpriteRenderer>();

            TextMesh title =
                titleTransform.GetComponent<TextMesh>();

            TextMesh content =
                contentTransform.GetComponent<TextMesh>();

            TextMesh overNum =
                overNumTransform.GetComponent<TextMesh>();

            if (icon == null ||
                title == null ||
                content == null ||
                overNum == null)
            {
                Destroy(obj);
                continue;
            }

            LoadedAcvRederer.Add(icon);
            LoadedAcvOverNum.Add(overNum);

            overNum.text = "";

            icon.color =
                new Color(
                    1f,
                    1f,
                    1f,
                    0.4f
                );

            Acvname achievement =
                achievementList[i];

            title.text =
                GetAchivName(achievement);

            content.text =
                GetAchivContent(achievement);

            if (AcvIcons != null &&
                i < AcvIcons.Count &&
                AcvIcons[i] != null)
            {
                icon.sprite = AcvIcons[i];
            }
            else
            {
                icon.sprite = EmptyIcon;
            }
        }
    }

    private void UpdateAchievementUI()
    {
        List<int> save = OverAcvmentNum;

        if (save == null)
            return;

        int count =
            Mathf.Min(
                achievementList.Count,
                LoadedAcvRederer.Count
            );

        for (int i = 0; i < count; i++)
        {
            int index =
                (int)achievementList[i];

            int num =
                index >= 0 && index < save.Count
                    ? save[index]
                    : 0;

            LoadedAcvRederer[i].color =
                num > 0
                    ? Color.white
                    : new Color(
                        1f,
                        1f,
                        1f,
                        0.4f
                    );

            LoadedAcvOverNum[i].text =
                num > 1
                    ? num.ToString()
                    : "";
        }
    }

    private string GetAchivName(Acvname acvname)
    {
        switch (acvname)
        {
            case Acvname.Potato5:
                return "Puréc de papa";

            case Acvname.Cherry25:
                return "Hermanos explosivos";

            case Acvname.Popcorn4:
                return "Palomitas de maíz";

            case Acvname.Squash10:
                return "Golpe aplastante";

            case Acvname.ClickMoney100:
                return "Tacaño";

            case Acvname.SunFull:
                return "Casa llena de sol";

            case Acvname.RollNut5:
                return "Cinco estrellas";

            case Acvname.Plant49:
                return "Cazador de plantas";

            case Acvname.UnitAsOne:
                return "Todos como uno";

            case Acvname.Sunflower200:
                return "Brillo deslumbrante";

            case Acvname.Money100w:
                return "Millonario";

            case Acvname.LuckCorn5:
                return "Maíz de la suerte";

            case Acvname.UltimateKill:
                return "Eliminación extrema";

            case Acvname.CheatSquash50:
                return "El arte del engaño";

            case Acvname.ChaosZombie:
                return "Zombi caótico";

            case Acvname.FlatSquash:
                return "Aplastado al revés";

            case Acvname.IceShroom20:
                return "Asesino de sangre fría";

            default:
                return "???";
        }
    }

    private string GetAchivContent(Acvname acvname)
    {
        switch (acvname)
        {
            case Acvname.Potato5:
                return "Haz que una Mina de patata explote y lance por los aires a 5 zombis de una vez.";

            case Acvname.Cherry25:
                return "Mata con una sola Bomba cereza a 25 zombis normales al mismo tiempo.";

            case Acvname.Popcorn4:
                return "Mata con un solo proyectil de Mazorcañón a 4 zombis gigantes al mismo tiempo.";

            case Acvname.Squash10:
                return "Aplasta a 10 zombis de una vez con una Calabaza.";

            case Acvname.ClickMoney100:
                return "Recoge dinero 100 veces seguidas sin dejar que ninguna moneda desaparezca.";

            case Acvname.SunFull:
                return "Gana un nivel teniendo 10000 o más soles restantes.";

            case Acvname.RollNut5:
                return "Haz que una Nuez derribe a 5 zombis.";

            case Acvname.Plant49:
                return "Consigue 49 plantas.";

            case Acvname.UnitAsOne:
                return "Completa un nivel de tres mapas junto con otros tres jugadores.";

            case Acvname.Sunflower200:
                return "Haz que un Girasol produzca 200 soles o más de una vez.";

            case Acvname.Money100w:
                return "Consigue un millón de monedas.";

            case Acvname.LuckCorn5:
                return "Haz que un Lanzamaíz lance 5 proyectiles de mantequilla seguidos.";

            case Acvname.UltimateKill:
                return "Mata a un zombi justo cuando está a punto de entrar en tu casa. (Actualmente no disponible)";

            case Acvname.CheatSquash50:
                return "Haz que una Calabaza sea engañada 50 veces. (Actualmente no disponible)";

            case Acvname.ChaosZombie:
                return "Haz que un zombi tenga al mismo tiempo los efectos de encantamiento, mantequilla, aturdimiento y congelación.";

            case Acvname.FlatSquash:
                return "Haz que una Calabaza sea aplastada.";

            case Acvname.IceShroom20:
                return "Congela hasta la muerte a 20 zombis normales usando una Hielaguisante.";

            default:
                return "???";
        }
    }
}