using System.Collections.Generic;
using SocketSave;
using UnityEngine;

public class BattlePlayerList : MonoBehaviour
{
    public static BattlePlayerList Instance;

    public PlayerShow HostShow;
    public PlayerShow Player2Show;
    public PlayerShow Player3Show;
    public PlayerShow Player4Show;

    public List<Animator> ShovelAnims = new List<Animator>();
    public List<Animator> HammerAnims = new List<Animator>();

    private int ShovelAnimNum;
    private int HammerAnimNum;

    // Datos recibidos mientras el objeto estaba desactivado.
    private List<string> pendingPlayers;
    private List<int> pendingCardNum;

    private bool hasPendingPlayerData;

    private void Awake()
    {
        Instance = this;
        ClearPlayerList();
    }

    private void OnEnable()
    {
        Instance = this;

        if (hasPendingPlayerData)
        {
            hasPendingPlayerData = false;

            LoadAllSeedBankInternal(
                pendingPlayers,
                pendingCardNum
            );

            pendingPlayers = null;
            pendingCardNum = null;
        }
    }

    private void ClearPlayerList()
    {
        HidePlayer(HostShow);
        HidePlayer(Player2Show);
        HidePlayer(Player3Show);
        HidePlayer(Player4Show);
    }

    private PlayerShow GetPlayerShow(string playerName)
    {
        if (HostShow != null &&
            HostShow.nameText != null &&
            HostShow.nameText.text == playerName)
        {
            return HostShow;
        }

        if (Player2Show != null &&
            Player2Show.nameText != null &&
            Player2Show.nameText.text == playerName)
        {
            return Player2Show;
        }

        if (Player3Show != null &&
            Player3Show.nameText != null &&
            Player3Show.nameText.text == playerName)
        {
            return Player3Show;
        }

        if (Player4Show != null &&
            Player4Show.nameText != null &&
            Player4Show.nameText.text == playerName)
        {
            return Player4Show;
        }

        return null;
    }

    private void ShowPlayer(
        PlayerShow show,
        string playerName)
    {
        if (show == null)
            return;

        show.gameObject.SetActive(true);

        if (show.nameText != null)
            show.nameText.text = playerName;

        if (show.GridSeletorName != null)
            show.GridSeletorName.text = playerName;

        show.IsPrepare = false;
    }

    private void HidePlayer(PlayerShow show)
    {
        if (show == null)
            return;

        ClearPreview(show);

        if (show.nameText != null)
            show.nameText.text = "";

        if (show.GridSeletorName != null)
            show.GridSeletorName.text = "";

        show.IsPrepare = false;

        if (show.SeedBank != null)
            show.SeedBank.ClearCardSlot();

        show.gameObject.SetActive(false);
    }

    public void LoadAllSeedBank(
        List<string> players,
        List<int> cardNum)
    {
        // Si el BattlePlayerList todavía está desactivado,
        // guardamos los datos para procesarlos al activarse.
        if (!gameObject.activeInHierarchy)
        {
            pendingPlayers =
                players != null
                    ? new List<string>(players)
                    : null;

            pendingCardNum =
                cardNum != null
                    ? new List<int>(cardNum)
                    : null;

            hasPendingPlayerData = true;

            return;
        }

        LoadAllSeedBankInternal(
            players,
            cardNum
        );
    }

    private void LoadAllSeedBankInternal(
        List<string> players,
        List<int> cardNum)
    {
        if (Player2Show != null)
            Player2Show.IsPrepare = false;

        if (Player3Show != null)
            Player3Show.IsPrepare = false;

        if (Player4Show != null)
            Player4Show.IsPrepare = false;

        if (Player2Show != null)
            Player2Show.transform.SetAsLastSibling();

        if (Player3Show != null)
            Player3Show.transform.SetAsLastSibling();

        if (Player4Show != null)
            Player4Show.transform.SetAsLastSibling();

        if (players == null || cardNum == null)
            return;

        int count = Mathf.Min(
            players.Count,
            cardNum.Count
        );

        // Primero mostramos los jugadores.
        for (int i = 0; i < count; i++)
        {
            string playerName = players[i];

            if (string.IsNullOrEmpty(playerName))
                continue;

            PlayerShow show =
                GetPlayerShow(playerName);

            if (show != null)
            {
                if (!show.gameObject.activeSelf)
                    ShowPlayer(
                        show,
                        playerName
                    );
            }
        }

        // Si todavía no existen los PlayerShow,
        // asignamos los jugadores a los slots disponibles.
        for (int i = 0; i < count; i++)
        {
            string playerName = players[i];

            if (string.IsNullOrEmpty(playerName))
                continue;

            if (GetPlayerShow(playerName) != null)
                continue;

            if (Player2Show != null &&
                !Player2Show.gameObject.activeSelf)
            {
                ShowPlayer(
                    Player2Show,
                    playerName
                );
            }
            else if (Player3Show != null &&
                     !Player3Show.gameObject.activeSelf)
            {
                ShowPlayer(
                    Player3Show,
                    playerName
                );
            }
            else if (Player4Show != null &&
                     !Player4Show.gameObject.activeSelf)
            {
                ShowPlayer(
                    Player4Show,
                    playerName
                );
            }
        }

        // Ahora que los PlayerShow ya tienen nombre,
        // podemos crear sus SeedBank.
        for (int i = 0; i < count; i++)
        {
            string playerName = players[i];

            if (string.IsNullOrEmpty(playerName))
                continue;

            PlayerShow show =
                GetPlayerShow(playerName);

            if (show == null ||
                show.SeedBank == null)
            {
                continue;
            }

            show.SeedBank.SpawnCardSlot(
                cardNum[i]
            );
        }

        UpdateAllMapSprites();
    }

    private void UpdateAllMapSprites()
    {
        if (MapManager.Instance == null ||
            MapManager.Instance.mapList == null ||
            MapManager.Instance.mapList.Count == 0)
        {
            return;
        }

        MapBase map =
            MapManager.Instance.mapList[0];

        if (map == null)
            return;

        Sprite mapSprite =
            map.GotoSprite;

        UpdateMapSprite(
            HostShow,
            mapSprite
        );

        UpdateMapSprite(
            Player2Show,
            mapSprite
        );

        UpdateMapSprite(
            Player3Show,
            mapSprite
        );

        UpdateMapSprite(
            Player4Show,
            mapSprite
        );
    }

    private void UpdateMapSprite(
        PlayerShow show,
        Sprite mapSprite)
    {
        if (show == null ||
            show.MapSprite == null ||
            !show.gameObject.activeSelf)
        {
            return;
        }

        show.MapSprite.sprite = mapSprite;
    }

    public void PreviewPlant(PlantPreview apply)
    {
        if (apply == null)
            return;

        if (GameManager.Instance == null ||
            GameManager.Instance.LocalPlayerSave == null ||
            LV.Instance == null)
        {
            return;
        }

        if (apply.PlayerName ==
            GameManager.Instance.LocalPlayerSave.playerName ||
            (LV.Instance.CurrLVType == LVType.PvP &&
             PvPSelector.Instance != null &&
             !PvPSelector.Instance.IsSameTeam(
                 apply.PlayerName)))
        {
            return;
        }

        PlayerShow show =
            GetPlayerShow(apply.PlayerName);

        if (show == null)
            return;

        if (apply.plantType == PlantType.Nope)
        {
            if (show.plantInGrid != null)
            {
                show.plantInGrid.Dead(
                    isFlat: false,
                    0f,
                    synClient: true,
                    deadRattle: false
                );

                show.plantInGrid = null;
            }

            if (show.GridSeletor != null)
                show.GridSeletor.gameObject.SetActive(false);

            return;
        }

        if (MapManager.Instance == null)
            return;

        Grid grid =
            MapManager.Instance.GetGridByWorldPos(
                apply.GridPos
            );

        if (grid == null)
            return;

        if (show.GridSeletor != null)
        {
            show.GridSeletor.gameObject.SetActive(true);

            show.GridSeletor.transform.position =
                grid.Position +
                new Vector2(-0.5f, 0.3f);
        }

        if (show.plantInGrid == null)
        {
            if (PlantManager.Instance == null)
                return;

            show.plantInGrid =
                PlantManager.Instance.GetNewPlant(
                    apply.plantType
                );

            if (show.plantInGrid == null)
                return;

            show.plantInGrid.transform.SetParent(
                PlantManager.Instance.transform
            );

            show.plantInGrid.InitForCreate(
                inGrid: true,
                grid,
                apply.isImtor
            );
        }
        else
        {
            show.plantInGrid.UpdateForCreate(grid);
        }
    }

    public void PreviewZombie(ZombiePreview apply)
    {
        if (apply == null)
            return;

        if (GameManager.Instance == null ||
            GameManager.Instance.LocalPlayerSave == null ||
            LV.Instance == null)
        {
            return;
        }

        if (apply.PlayerName ==
            GameManager.Instance.LocalPlayerSave.playerName ||
            (LV.Instance.CurrLVType == LVType.PvP &&
             PvPSelector.Instance != null &&
             !PvPSelector.Instance.IsSameTeam(
                 apply.PlayerName)))
        {
            return;
        }

        PlayerShow show =
            GetPlayerShow(apply.PlayerName);

        if (show == null)
            return;

        if (apply.zombieType == ZombieType.Nope)
        {
            if (show.zombieInGrid != null)
            {
                show.zombieInGrid.DirectDead(
                    canDropItem: false,
                    0f,
                    synClient: true
                );

                show.zombieInGrid = null;
            }

            if (show.GridSeletor != null)
                show.GridSeletor.gameObject.SetActive(false);

            return;
        }

        if (MapManager.Instance == null)
            return;

        Grid grid =
            MapManager.Instance.GetGridByWorldPos(
                apply.GridPos
            );

        if (grid == null)
            return;

        if (show.GridSeletor != null)
        {
            show.GridSeletor.gameObject.SetActive(true);

            show.GridSeletor.transform.position =
                grid.Position +
                new Vector2(-0.5f, 0.3f);
        }

        if (show.zombieInGrid == null)
        {
            if (ZombieManager.Instance == null ||
                PlantManager.Instance == null)
            {
                return;
            }

            show.zombieInGrid =
                ZombieManager.Instance.GetNewZombie(
                    apply.zombieType
                );

            if (show.zombieInGrid == null)
                return;

            show.zombieInGrid.transform.SetParent(
                PlantManager.Instance.transform
            );

            show.zombieInGrid.CreateInit(
                inGrid: true,
                grid,
                apply.isRat
            );
        }
        else
        {
            show.zombieInGrid.UpdateForCreate(grid);
        }
    }

    public void PreviewShovel(
        string playerName,
        Vector2 pos,
        bool isShow)
    {
        if (GameManager.Instance == null ||
            GameManager.Instance.LocalPlayerSave == null ||
            LV.Instance == null)
        {
            return;
        }

        if (playerName ==
            GameManager.Instance.LocalPlayerSave.playerName ||
            (LV.Instance.CurrLVType == LVType.PvP &&
             PvPSelector.Instance != null &&
             !PvPSelector.Instance.IsSameTeam(playerName)))
        {
            return;
        }

        PlayerShow show =
            GetPlayerShow(playerName);

        if (show == null)
            return;

        if (!isShow)
        {
            if (show.GridSeletor != null)
                show.GridSeletor.gameObject.SetActive(false);

            return;
        }

        if (MapManager.Instance == null)
            return;

        Grid grid =
            MapManager.Instance.GetGridByWorldPos(pos);

        if (grid == null)
            return;

        if (show.GridSeletor != null)
        {
            show.GridSeletor.gameObject.SetActive(true);

            show.GridSeletor.transform.position =
                grid.Position +
                new Vector2(-0.5f, 0.3f);
        }
    }

    public void PlayShovelAnimation(
        Vector2 gridPos,
        int sound,
        string playerName)
    {
        if (LV.Instance == null ||
            MapManager.Instance == null)
        {
            return;
        }

        if (LV.Instance.CurrLVType == LVType.PvP &&
            PvPSelector.Instance != null &&
            !PvPSelector.Instance.IsSameTeam(playerName))
        {
            return;
        }

        Grid grid =
            MapManager.Instance.GetGridByWorldPos(
                gridPos
            );

        if (grid == null ||
            ShovelAnims.Count == 0)
        {
            return;
        }

        ShovelAnimNum++;

        if (ShovelAnimNum >= ShovelAnims.Count)
            ShovelAnimNum = 0;

        Animator anim =
            ShovelAnims[ShovelAnimNum];

        if (anim == null)
            return;

        anim.transform.position =
            grid.Position +
            new Vector2(0.5f, 0.5f);

        anim.Play(
            "Shovel",
            0,
            0f
        );

        if (sound == 1)
        {
            if (AudioManager.Instance == null ||
                GameManager.Instance == null ||
                GameManager.Instance.AudioConf == null)
            {
                return;
            }

            if (Random.Range(1, 3) == 1)
            {
                AudioManager.Instance.PlayEFAudio(
                    GameManager.Instance.AudioConf.Plant1,
                    anim.transform.position
                );
            }
            else
            {
                AudioManager.Instance.PlayEFAudio(
                    GameManager.Instance.AudioConf.Plant2,
                    anim.transform.position
                );
            }
        }
        else if (sound == 2)
        {
            if (AudioManager.Instance == null ||
                GameManager.Instance == null ||
                GameManager.Instance.AudioConf == null)
            {
                return;
            }

            if (Random.Range(1, 3) == 1)
            {
                AudioManager.Instance.PlayEFAudio(
                    GameManager.Instance.AudioConf.shoveling_snow1,
                    anim.transform.position
                );
            }
            else
            {
                AudioManager.Instance.PlayEFAudio(
                    GameManager.Instance.AudioConf.shoveling_snow2,
                    anim.transform.position
                );
            }
        }
    }

    public void PlayHammerAnimation(
        Vector2 pos,
        int type,
        string playerName)
    {
        if (LV.Instance == null ||
            MapManager.Instance == null)
        {
            return;
        }

        if (LV.Instance.CurrLVType == LVType.PvP &&
            PvPSelector.Instance != null &&
            !PvPSelector.Instance.IsSameTeam(playerName))
        {
            return;
        }

        if (HammerAnims.Count == 0)
            return;

        if (MapManager.Instance.GetGridByWorldPos(pos) == null)
            return;

        HammerAnimNum++;

        if (HammerAnimNum >= HammerAnims.Count)
            HammerAnimNum = 0;

        Animator anim =
            HammerAnims[HammerAnimNum];

        if (anim == null)
            return;

        anim.transform.position =
            pos +
            new Vector2(0.24f, -0.45f);

        if (type == 1)
        {
            anim.Play(
                "anim_open_pot",
                0,
                0f
            );
        }
        else
        {
            anim.Play(
                "anim_whack_zombie",
                0,
                0f
            );
        }

        if (AudioManager.Instance != null &&
            GameManager.Instance != null &&
            GameManager.Instance.AudioConf != null)
        {
            AudioManager.Instance.PlayEFAudio(
                GameManager.Instance.AudioConf.swing,
                transform.position
            );
        }
    }

    public void SelectCard(
        string playerName,
        PlantType type,
        ZombieType zType,
        bool noAnim)
    {
        PlayerShow show =
            GetPlayerShow(playerName);

        if (show == null ||
            show.SeedBank == null ||
            SeedBank.Instance == null ||
            LV.Instance == null)
        {
            return;
        }

        UIPlantCardNC card =
            type == PlantType.Nope
                ? SeedBank.Instance.GetZombieNc(zType)
                : SeedBank.Instance.GetPlantNc(type);

        if (card == null)
            return;

        bool sameTeam = true;

        if (LV.Instance.CurrLVType == LVType.PvP)
        {
            if (PvPSelector.Instance == null)
                return;

            sameTeam =
                PvPSelector.Instance.IsSameTeam(
                    show.nameText != null
                        ? show.nameText.text
                        : playerName
                );

            if (sameTeam)
                card.IsChoosed = true;
        }
        else
        {
            card.IsChoosed = true;
        }

        if (!card.IsUnLock)
            sameTeam = false;

        show.SeedBank.ChooseCard(
            card,
            sameTeam && !noAnim
        );
    }

    public void CancelCard(
        string playerName,
        int cardId)
    {
        PlayerShow show =
            GetPlayerShow(playerName);

        if (show == null ||
            show.SeedBank == null ||
            LV.Instance == null)
        {
            return;
        }

        bool needAnim = true;

        if (LV.Instance.CurrLVType == LVType.PvP)
        {
            if (PvPSelector.Instance == null)
                return;

            needAnim =
                PvPSelector.Instance.IsSameTeam(
                    show.nameText != null
                        ? show.nameText.text
                        : playerName
                );
        }

        show.SeedBank.ClearChoose(
            cardId,
            needAnim
        );
    }

    public bool CheckPrepare()
    {
        if (Player2Show != null &&
            Player2Show.nameText != null &&
            Player2Show.nameText.text != "" &&
            !Player2Show.IsPrepare)
        {
            return false;
        }

        if (Player3Show != null &&
            Player3Show.nameText != null &&
            Player3Show.nameText.text != "" &&
            !Player3Show.IsPrepare)
        {
            return false;
        }

        if (Player4Show != null &&
            Player4Show.nameText != null &&
            Player4Show.nameText.text != "" &&
            !Player4Show.IsPrepare)
        {
            return false;
        }

        return true;
    }

    public void UpdateMapSprite(
        string playerName,
        Vector2 pos)
    {
        if (MapManager.Instance == null)
            return;

        MapBase map =
            MapManager.Instance.GetCurrMap(pos);

        if (map == null)
            return;

        PlayerShow show =
            GetPlayerShow(playerName);

        if (show != null &&
            show.MapSprite != null)
        {
            show.MapSprite.sprite =
                map.GotoSprite;
        }
    }

    public void UpdateCardCD(
        string playerName,
        int cardID,
        bool isOk)
    {
        PlayerShow show =
            GetPlayerShow(playerName);

        if (show == null ||
            show.SeedBank == null)
        {
            return;
        }

        show.SeedBank.UpdateCD(
            cardID,
            isOk
        );
    }

    public void UpdateState(
        string playerName,
        bool isPrepare)
    {
        PlayerShow show =
            GetPlayerShow(playerName);

        if (show == null ||
            show == HostShow)
        {
            return;
        }

        show.IsPrepare = isPrepare;
    }

    public void UpdatePlayerList(
        PlayerInfo Host,
        List<PlayerInfo> players)
    {
        if (Host != null)
        {
            ShowPlayer(
                HostShow,
                Host.Name
            );
        }
        else
        {
            HidePlayer(HostShow);
        }

        if (players == null)
            return;

        UpdatePlayerSlot(
            Player2Show,
            players
        );

        UpdatePlayerSlot(
            Player3Show,
            players
        );

        UpdatePlayerSlot(
            Player4Show,
            players
        );

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i] == null)
                continue;

            string playerName =
                players[i].Name;

            if (Contain2(playerName))
                continue;

            if (Player2Show != null &&
                !Player2Show.gameObject.activeSelf)
            {
                ShowPlayer(
                    Player2Show,
                    playerName
                );
            }
            else if (Player3Show != null &&
                     !Player3Show.gameObject.activeSelf)
            {
                ShowPlayer(
                    Player3Show,
                    playerName
                );
            }
            else if (Player4Show != null &&
                     !Player4Show.gameObject.activeSelf)
            {
                ShowPlayer(
                    Player4Show,
                    playerName
                );
            }
        }
    }

    private void UpdatePlayerSlot(
        PlayerShow show,
        List<PlayerInfo> players)
    {
        if (show == null ||
            !show.gameObject.activeSelf)
        {
            return;
        }

        if (show.nameText == null ||
            !Contain(
                players,
                show.nameText.text
            ))
        {
            HidePlayer(show);
        }
    }

    private bool Contain(
        List<PlayerInfo> players,
        string name)
    {
        if (players == null)
            return false;

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i] != null &&
                players[i].Name == name)
            {
                return true;
            }
        }

        return false;
    }

    private bool Contain2(string name)
    {
        return
            (Player2Show != null &&
             Player2Show.gameObject.activeSelf &&
             Player2Show.nameText != null &&
             Player2Show.nameText.text == name) ||
            (Player3Show != null &&
             Player3Show.gameObject.activeSelf &&
             Player3Show.nameText != null &&
             Player3Show.nameText.text == name) ||
            (Player4Show != null &&
             Player4Show.gameObject.activeSelf &&
             Player4Show.nameText != null &&
             Player4Show.nameText.text == name);
    }

    private void ClearPreview(PlayerShow show)
    {
        if (show == null)
            return;

        if (show.plantInGrid != null)
        {
            show.plantInGrid.Dead(
                isFlat: false,
                0f,
                synClient: true,
                deadRattle: false
            );

            show.plantInGrid = null;
        }

        if (show.zombieInGrid != null)
        {
            show.zombieInGrid.DirectDead(
                canDropItem: false,
                0f,
                synClient: true
            );

            show.zombieInGrid = null;
        }

        if (show.GridSeletor != null)
        {
            show.GridSeletor.gameObject.SetActive(false);
        }
    }
}