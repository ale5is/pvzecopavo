using System;
using System.Collections.Generic;
using UnityEngine;

public class AcvmentManager : MonoBehaviour
{
    public static AcvmentManager Instance;

    public GameObject AchivPrefab;

    public Sprite EmptyIcon;

    public List<Sprite> AcvIcons = new List<Sprite>();

    private List<TextMesh> LoadedAcvOverNum = new List<TextMesh>();

    private List<SpriteRenderer> LoadedAcvRederer = new List<SpriteRenderer>();

    private List<Acvname> achievementList = new List<Acvname>
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

    private List<Acvname> CurrLvOverAcv = new List<Acvname>();

    private List<Acvname> GetOnlyOne = new List<Acvname>
    {
        Acvname.Plant49,
        Acvname.Money100w
    };

    private List<int> OverAcvmentNum => GameManager.Instance.StatsAcvSave.OverAcvNum;

    private void Awake()
    {
        Instance = this;
    }

    public void LoadAcvSave()
    {
        int length = Enum.GetValues(typeof(Acvname)).Length;

        while (OverAcvmentNum.Count < length)
        {
            OverAcvmentNum.Add(0);
        }
    }

    public void LVResetThis()
    {
        CurrLvOverAcv.Clear();
    }

    public void GetAchievement(Acvname achiv, string playerName)
    {
        if (playerName == GameManager.Instance.LocalPlayerSave.playerName)
        {
            GetAchievement(achiv);
        }
        else if (GameManager.Instance.isServer)
        {
            SocketServer.Instance.SendAcvmentGet(achiv, playerName);
        }
    }

    public void GetAchievement(Acvname achiv)
    {
        if (!CurrLvOverAcv.Contains(achiv))
        {
            CurrLvOverAcv.Add(achiv);

            if (OverAcvmentNum[(int)achiv] == 0)
            {
                StatsManager.Instance.AddStatsNum(StatsEnum.OverAcvNum);

                ChatInput.Instance.SendMessageToAll(
                    "Completó el logro <color=#41FF00>[" +
                    GetAchivName(achiv) +
                    "]</color>!",
                    needName: true
                );

                AudioManager.Instance.PlayEFAudio(
                    GameManager.Instance.AudioConf.Achivment,
                    Vector2.zero,
                    isAll: true
                );
            }

            OverAcvmentNum[(int)achiv]++;

            if (OverAcvmentNum[(int)achiv] == 1)
            {
                GameManager.Instance.SaveSAInfo();
            }

            if (GetOnlyOne.Contains(achiv))
            {
                OverAcvmentNum[(int)achiv] = 1;
            }
        }
    }

    public void InitAcvment()
    {
        if (LoadedAcvRederer.Count == 0)
        {
            for (int i = 0; i < achievementList.Count; i++)
            {
                GameObject obj = UnityEngine.Object.Instantiate(AchivPrefab);

                obj.transform.SetParent(base.transform);

                obj.transform.localPosition =
                    new Vector3(
                        0f,
                        -2.5f - 1.5f * (float)i
                    );

                SpriteRenderer component =
                    obj.transform.Find("AcvIcons").GetComponent<SpriteRenderer>();

                TextMesh component2 =
                    obj.transform.Find("AcvTitle").GetComponent<TextMesh>();

                TextMesh component3 =
                    obj.transform.Find("AcvContent").GetComponent<TextMesh>();

                TextMesh component4 =
                    obj.transform.Find("AcvOverNum").GetComponent<TextMesh>();

                LoadedAcvRederer.Add(component);
                LoadedAcvOverNum.Add(component4);

                component4.text = "";

                component.color =
                    new Color(1f, 1f, 1f, 0.4f);

                component2.text =
                    GetAchivName(achievementList[i]);

                component3.text =
                    GetAchivContent(achievementList[i]);

                if (AcvIcons.Count > i)
                {
                    component.sprite = AcvIcons[i];
                }
                else
                {
                    component.sprite = EmptyIcon;
                }
            }
        }

        for (int j = 0; j < achievementList.Count; j++)
        {
            int num =
                OverAcvmentNum[(int)achievementList[j]];

            if (num > 0)
            {
                LoadedAcvRederer[j].color = Color.white;
            }
            else
            {
                LoadedAcvRederer[j].color =
                    new Color(1f, 1f, 1f, 0.4f);
            }

            if (num > 1)
            {
                LoadedAcvOverNum[j].text =
                    num.ToString();
            }
            else
            {
                LoadedAcvOverNum[j].text = "";
            }
        }
    }

    private string GetAchivName(Acvname acvname)
    {
        if (acvname == Acvname.Potato5)
            return "Puréc de papa";

        if (acvname == Acvname.Cherry25)
            return "Hermanos explosivos";

        if (acvname == Acvname.Popcorn4)
            return "Palomitas de maíz";

        if (acvname == Acvname.Squash10)
            return "Golpe aplastante";

        if (acvname == Acvname.ClickMoney100)
            return "Tacaño";

        if (acvname == Acvname.SunFull)
            return "Casa llena de sol";

        if (acvname == Acvname.RollNut5)
            return "Cinco estrellas";

        if (acvname == Acvname.Plant49)
            return "Cazador de plantas";

        if (acvname == Acvname.UnitAsOne)
            return "Todos como uno";

        if (acvname == Acvname.Sunflower200)
            return "Brillo deslumbrante";

        if (acvname == Acvname.Money100w)
            return "Millonario";

        if (acvname == Acvname.LuckCorn5)
            return "Maíz de la suerte";

        if (acvname == Acvname.UltimateKill)
            return "Eliminación extrema";

        if (acvname == Acvname.CheatSquash50)
            return "El arte del engaño";

        if (acvname == Acvname.ChaosZombie)
            return "Zombi caótico";

        if (acvname == Acvname.FlatSquash)
            return "Aplastado al revés";

        if (acvname == Acvname.IceShroom20)
            return "Asesino de sangre fría";

        return "???";
    }

    private string GetAchivContent(Acvname acvname)
    {
        if (acvname == Acvname.Potato5)
            return "Haz que una Mina de patata explote y lance por los aires a 5 zombis de una vez.";

        if (acvname == Acvname.Cherry25)
            return "Mata con una sola Bomba cereza a 25 zombis normales al mismo tiempo.";

        if (acvname == Acvname.Popcorn4)
            return "Mata con un solo proyectil de Mazorcañón a 4 zombis gigantes al mismo tiempo.";

        if (acvname == Acvname.Squash10)
            return "Aplasta a 10 zombis de una vez con una Calabaza.";

        if (acvname == Acvname.ClickMoney100)
            return "Recoge dinero 100 veces seguidas sin dejar que ninguna moneda desaparezca.";

        if (acvname == Acvname.SunFull)
            return "Gana un nivel teniendo 10000 o más soles restantes.";

        if (acvname == Acvname.RollNut5)
            return "Haz que una Nuez derribe a 5 zombis.";

        if (acvname == Acvname.Plant49)
            return "Consigue 49 plantas.";

        if (acvname == Acvname.UnitAsOne)
            return "Completa un nivel de tres mapas junto con otros tres jugadores.";

        if (acvname == Acvname.Sunflower200)
            return "Haz que un Girasol produzca 200 soles o más de una vez.";

        if (acvname == Acvname.Money100w)
            return "Consigue un millón de monedas.";

        if (acvname == Acvname.LuckCorn5)
            return "Haz que un Lanzamaíz lance 5 proyectiles de mantequilla seguidos.";

        if (acvname == Acvname.UltimateKill)
            return "Mata a un zombi justo cuando está a punto de entrar en tu casa. (Actualmente no disponible)";

        if (acvname == Acvname.CheatSquash50)
            return "Haz que una Calabaza sea engañada 50 veces. (Actualmente no disponible)";

        if (acvname == Acvname.ChaosZombie)
            return "Haz que un zombi tenga al mismo tiempo los efectos de encantamiento, mantequilla, aturdimiento y congelación.";

        if (acvname == Acvname.FlatSquash)
            return "Haz que una Calabaza sea aplastada.";

        if (acvname == Acvname.IceShroom20)
            return "Congela hasta la muerte a 20 zombis normales usando una Hielaguisante.";

        return "???";
    }
}