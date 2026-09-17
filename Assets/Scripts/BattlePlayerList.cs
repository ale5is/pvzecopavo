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

    public List<Animator> ShovelAnims = new();
    public List<Animator> HammerAnims = new();

    private int ShovelAnimNum;
    private int HammerAnimNum;

    private List<string> pendingPlayers;
    private List<int> pendingCardNum;
    private bool hasPendingPlayerData;

    private void Awake()
    {
        Instance = this;
        ClearPlayerList();
        SetPlayerShowOrder();
    }

    private void OnEnable()
    {
        Instance = this;
        SetPlayerShowOrder();

        if (!hasPendingPlayerData)
            return;

        hasPendingPlayerData = false;

        LoadAllSeedBankInternal(
            pendingPlayers,
            pendingCardNum
        );

        pendingPlayers = null;
        pendingCardNum = null;
    }

    private void ClearPlayerList()
    {
        HidePlayer(HostShow);
        HidePlayer(Player2Show);
        HidePlayer(Player3Show);
        HidePlayer(Player4Show);
    }

    private void SetPlayerShowOrder()
    {
        if (HostShow != null)
            HostShow.transform.SetAsFirstSibling();

        if (Player2Show != null)
            Player2Show.transform.SetAsLastSibling();

        if (Player3Show != null)
            Player3Show.transform.SetAsLastSibling();

        if (Player4Show != null)
            Player4Show.transform.SetAsLastSibling();
    }

    private PlayerShow GetPlayerShow(string playerName)
    {
        if (string.IsNullOrEmpty(playerName))
            return null;

        if (IsPlayer(HostShow, playerName))
            return HostShow;

        if (IsPlayer(Player2Show, playerName))
            return Player2Show;

        if (IsPlayer(Player3Show, playerName))
            return Player3Show;

        if (IsPlayer(Player4Show, playerName))
            return Player4Show;

        if (IsHostPlayer(playerName))
        {
            if (HostShow != null)
                ShowPlayer(HostShow, playerName);

            return HostShow;
        }

        return null;
    }

    private bool IsPlayer(
        PlayerShow show,
        string playerName)
    {
        return show != null &&
               show.nameText != null &&
               show.nameText.text == playerName;
    }

    private bool IsHostPlayer(string playerName)
    {
        if (string.IsNullOrEmpty(playerName))
            return false;

        if (IsPlayer(HostShow, playerName))
            return true;

        if (GameManager.Instance != null &&
            GameManager.Instance.LocalPlayerSave != null &&
            GameManager.Instance.isServer &&
            !GameManager.Instance.isClient)
        {
            return GameManager.Instance.LocalPlayerSave.playerName == playerName;
        }

        return false;
    }

    private string GetLocalHostName()
    {
        if (GameManager.Instance == null ||
            GameManager.Instance.LocalPlayerSave == null)
        {
            return null;
        }

        if (!GameManager.Instance.isServer ||
            GameManager.Instance.isClient)
        {
            return null;
        }

        return GameManager.Instance.LocalPlayerSave.playerName;
    }

    private void EnsureHostShow()
    {
        string hostName = GetLocalHostName();

        if (string.IsNullOrEmpty(hostName))
            return;

        ShowPlayer(HostShow, hostName);

        if (HostShow != null)
            HostShow.transform.SetAsFirstSibling();
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

        if (show.SeedBank != null)
            show.SeedBank.OwnerShow = show;
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
        {
            show.SeedBank.OwnerShow = show;
            show.SeedBank.ClearCardSlot();
        }

        show.gameObject.SetActive(false);
    }

    public void LoadAllSeedBank(
        List<string> players,
        List<int> cardNum)
    {
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

        LoadAllSeedBankInternal(players, cardNum);
    }

    private void LoadAllSeedBankInternal(
        List<string> players,
        List<int> cardNum)
    {
        ResetPlayerOrder();
        EnsureHostShow();

        if (players == null || cardNum == null)
            return;

        int count = Mathf.Min(
            players.Count,
            cardNum.Count
        );

        if (count <= 0)
            return;

        AssignPlayers(players, count);
        CreatePlayerSeedBanks(players, cardNum, count);
        UpdateAllMapSprites();
        SetPlayerShowOrder();
    }

    private void ResetPlayerOrder()
    {
        if (HostShow != null)
        {
            HostShow.IsPrepare = false;
            HostShow.transform.SetAsFirstSibling();
        }

        if (Player2Show != null)
        {
            Player2Show.IsPrepare = false;
            Player2Show.transform.SetAsLastSibling();
        }

        if (Player3Show != null)
        {
            Player3Show.IsPrepare = false;
            Player3Show.transform.SetAsLastSibling();
        }

        if (Player4Show != null)
        {
            Player4Show.IsPrepare = false;
            Player4Show.transform.SetAsLastSibling();
        }
    }

    private void AssignPlayers(
        List<string> players,
        int count)
    {
        for (int i = 0; i < count; i++)
        {
            string playerName = players[i];

            if (string.IsNullOrEmpty(playerName))
                continue;

            if (IsHostPlayer(playerName))
            {
                ShowPlayer(HostShow, playerName);

                if (HostShow != null)
                    HostShow.transform.SetAsFirstSibling();

                continue;
            }

            PlayerShow existing = GetPlayerShow(playerName);

            if (existing != null)
            {
                ShowPlayer(existing, playerName);
                continue;
            }

            AssignPlayerToFreeSlot(playerName);
        }
    }

    private void AssignPlayerToFreeSlot(string playerName)
    {
        if (string.IsNullOrEmpty(playerName) ||
            IsHostPlayer(playerName))
        {
            return;
        }

        if (TryAssign(Player2Show, playerName))
            return;

        if (TryAssign(Player3Show, playerName))
            return;

        TryAssign(Player4Show, playerName);
    }

    private bool TryAssign(
        PlayerShow show,
        string playerName)
    {
        if (show == null ||
            show.gameObject.activeSelf)
        {
            return false;
        }

        ShowPlayer(show, playerName);
        return true;
    }

    private void CreatePlayerSeedBanks(
        List<string> players,
        List<int> cardNum,
        int count)
    {
        for (int i = 0; i < count; i++)
        {
            string playerName = players[i];

            if (string.IsNullOrEmpty(playerName))
                continue;

            PlayerShow show = GetPlayerShow(playerName);

            if (show == null && IsHostPlayer(playerName))
            {
                ShowPlayer(HostShow, playerName);
                show = HostShow;
            }

            if (show == null ||
                show.SeedBank == null)
            {
                continue;
            }

            show.SeedBank.OwnerShow = show;

            int amount = Mathf.Max(0, cardNum[i]);

            show.SeedBank.SpawnCardSlot(amount);
        }
    }

    private void UpdateAllMapSprites()
    {
        if (MapManager.Instance == null ||
            MapManager.Instance.mapList == null ||
            MapManager.Instance.mapList.Count == 0)
        {
            return;
        }

        MapBase map = MapManager.Instance.mapList[0];

        if (map == null)
            return;

        Sprite sprite = map.GotoSprite;

        UpdateMapSprite(HostShow, sprite);
        UpdateMapSprite(Player2Show, sprite);
        UpdateMapSprite(Player3Show, sprite);
        UpdateMapSprite(Player4Show, sprite);
    }

    private void UpdateMapSprite(
        PlayerShow show,
        Sprite sprite)
    {
        if (show == null ||
            show.MapSprite == null ||
            !show.gameObject.activeSelf)
        {
            return;
        }

        show.MapSprite.sprite = sprite;
    }

    public void PreviewPlant(PlantPreview apply)
    {
        if (!CanPreview(apply))
            return;

        PlayerShow show = GetPlayerShow(apply.PlayerName);

        if (show == null)
            return;

        if (apply.plantType == PlantType.Nope)
        {
            ClearPlantPreview(show);
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

        ShowGridSelector(show, grid);

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
                true,
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
        if (!CanPreview(apply))
            return;

        PlayerShow show = GetPlayerShow(apply.PlayerName);

        if (show == null)
            return;

        if (apply.zombieType == ZombieType.Nope)
        {
            ClearZombiePreview(show);
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

        ShowGridSelector(show, grid);

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
                true,
                grid,
                apply.isRat
            );
        }
        else
        {
            show.zombieInGrid.UpdateForCreate(grid);
        }
    }

    private bool CanPreview(PlantPreview apply)
    {
        if (apply == null ||
            GameManager.Instance == null ||
            GameManager.Instance.LocalPlayerSave == null ||
            LV.Instance == null)
        {
            return false;
        }

        string localName =
            GameManager.Instance.LocalPlayerSave.playerName;

        if (apply.PlayerName == localName)
            return false;

        return LV.Instance.CurrLVType != LVType.PvP ||
               PvPSelector.Instance == null ||
               PvPSelector.Instance.IsSameTeam(
                   apply.PlayerName
               );
    }

    private bool CanPreview(ZombiePreview apply)
    {
        if (apply == null ||
            GameManager.Instance == null ||
            GameManager.Instance.LocalPlayerSave == null ||
            LV.Instance == null)
        {
            return false;
        }

        string localName =
            GameManager.Instance.LocalPlayerSave.playerName;

        if (apply.PlayerName == localName)
            return false;

        return LV.Instance.CurrLVType != LVType.PvP ||
               PvPSelector.Instance == null ||
               PvPSelector.Instance.IsSameTeam(
                   apply.PlayerName
               );
    }

    private void ShowGridSelector(
        PlayerShow show,
        Grid grid)
    {
        if (show == null ||
            show.GridSeletor == null)
        {
            return;
        }

        show.GridSeletor.gameObject.SetActive(true);

        show.GridSeletor.transform.position =
            grid.Position +
            new Vector2(-0.5f, 0.3f);
    }

    private void ClearPlantPreview(PlayerShow show)
    {
        if (show.plantInGrid != null)
        {
            show.plantInGrid.Dead(
                false,
                0f,
                true,
                false
            );

            show.plantInGrid = null;
        }

        HideGridSelector(show);
    }

    private void ClearZombiePreview(PlayerShow show)
    {
        if (show.zombieInGrid != null)
        {
            show.zombieInGrid.DirectDead(
                false,
                0f,
                true
            );

            show.zombieInGrid = null;
        }

        HideGridSelector(show);
    }

    private void HideGridSelector(PlayerShow show)
    {
        if (show != null &&
            show.GridSeletor != null)
        {
            show.GridSeletor.gameObject.SetActive(false);
        }
    }

    public void PreviewShovel(
        string playerName,
        Vector2 pos,
        bool isShow)
    {
        if (!CanUsePlayerPreview(playerName))
            return;

        PlayerShow show = GetPlayerShow(playerName);

        if (show == null)
            return;

        if (!isShow)
        {
            HideGridSelector(show);
            return;
        }

        if (MapManager.Instance == null)
            return;

        Grid grid =
            MapManager.Instance.GetGridByWorldPos(pos);

        if (grid == null)
            return;

        ShowGridSelector(show, grid);
    }

    public void PlayShovelAnimation(
        Vector2 gridPos,
        int sound,
        string playerName)
    {
        if (!CanUsePlayerPreview(playerName) ||
            MapManager.Instance == null ||
            ShovelAnims.Count == 0)
        {
            return;
        }

        Grid grid =
            MapManager.Instance.GetGridByWorldPos(
                gridPos
            );

        if (grid == null)
            return;

        Animator anim =
            GetNextAnimator(
                ShovelAnims,
                ref ShovelAnimNum
            );

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

        PlayShovelSound(
            anim.transform.position,
            sound
        );
    }

    private Animator GetNextAnimator(
        List<Animator> animators,
        ref int index)
    {
        if (animators == null ||
            animators.Count == 0)
        {
            return null;
        }

        index++;

        if (index >= animators.Count)
            index = 0;

        return animators[index];
    }

    private void PlayShovelSound(
        Vector3 position,
        int sound)
    {
        if (sound != 1 && sound != 2)
            return;

        if (AudioManager.Instance == null ||
            GameManager.Instance == null ||
            GameManager.Instance.AudioConf == null)
        {
            return;
        }

        bool first = Random.Range(0, 2) == 0;

        if (sound == 1)
        {
            AudioManager.Instance.PlayEFAudio(
                first
                    ? GameManager.Instance.AudioConf.Plant1
                    : GameManager.Instance.AudioConf.Plant2,
                position
            );
        }
        else
        {
            AudioManager.Instance.PlayEFAudio(
                first
                    ? GameManager.Instance.AudioConf.shoveling_snow1
                    : GameManager.Instance.AudioConf.shoveling_snow2,
                position
            );
        }
    }

    public void PlayHammerAnimation(
        Vector2 pos,
        int type,
        string playerName)
    {
        if (!CanUsePlayerPreview(playerName) ||
            MapManager.Instance == null ||
            HammerAnims.Count == 0)
        {
            return;
        }

        if (MapManager.Instance.GetGridByWorldPos(pos) == null)
            return;

        Animator anim =
            GetNextAnimator(
                HammerAnims,
                ref HammerAnimNum
            );

        if (anim == null)
            return;

        anim.transform.position =
            pos +
            new Vector2(0.24f, -0.45f);

        anim.Play(
            type == 1
                ? "anim_open_pot"
                : "anim_whack_zombie",
            0,
            0f
        );

        if (AudioManager.Instance != null &&
            GameManager.Instance != null &&
            GameManager.Instance.AudioConf != null)
        {
            AudioManager.Instance.PlayEFAudio(
                GameManager.Instance.AudioConf.swing,
                anim.transform.position
            );
        }
    }

    private bool CanUsePlayerPreview(string playerName)
    {
        if (LV.Instance == null ||
            GameManager.Instance == null ||
            GameManager.Instance.LocalPlayerSave == null)
        {
            return false;
        }

        if (playerName ==
            GameManager.Instance.LocalPlayerSave.playerName)
        {
            return false;
        }

        return LV.Instance.CurrLVType != LVType.PvP ||
               PvPSelector.Instance == null ||
               PvPSelector.Instance.IsSameTeam(playerName);
    }

    public void SelectCard(
    string playerName,
    PlantType plantType,
    ZombieType zombieType,
    bool noAnim,
    int cardId = -1)
    {
        PlayerShow show = GetPlayerShow(playerName);

        if (show == null ||
            show.SeedBank == null)
        {
            return;
        }

        UIPlantCardNC card = null;

        if (SeedBank.Instance != null)
        {
            card = SeedBank.Instance.GetPlantNc(plantType);

            if (card == null &&
                zombieType != ZombieType.Nope)
            {
                card = SeedBank.Instance.GetZombieNc(zombieType);
            }
        }

        if (card == null)
            return;

        bool sameTeam =
            LV.Instance == null ||
            LV.Instance.CurrLVType != LVType.PvP ||
            PvPSelector.Instance == null ||
            PvPSelector.Instance.IsSameTeam(playerName);

        show.SeedBank.ChooseCard(
            card,
            sameTeam && !noAnim,
            cardId
        );

        if (IsLocalHostPlayer(playerName) &&
            SocketServer.Instance != null)
        {
            SelectCard selectCard = new SelectCard
            {
                PlayerName = playerName,
                plantType = plantType,
                zombieType = zombieType,
                isBack = false,
                noAnim = noAnim,
                cardId = cardId
            };

            SocketServer.Instance.SelectCard(
                selectCard
            );
        }
    }
    public void CancelCard(
        string playerName,
        int cardId)
    {
        PlayerShow show = GetPlayerShow(playerName);

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

        if (IsLocalHostPlayer(playerName) &&
            SocketServer.Instance != null)
        {
            SocketServer.Instance.SelectCard(
                new SelectCard
                {
                    PlayerName = playerName,
                    cardId = cardId,
                    isBack = true,
                    noAnim = !needAnim
                }
            );
        }
    }

    private bool IsLocalHostPlayer(string playerName)
    {
        if (string.IsNullOrEmpty(playerName) ||
            GameManager.Instance == null ||
            GameManager.Instance.LocalPlayerSave == null)
        {
            return false;
        }

        if (!GameManager.Instance.isServer ||
            GameManager.Instance.isClient)
        {
            return false;
        }

        return playerName ==
               GameManager.Instance.LocalPlayerSave.playerName;
    }

    public bool CheckPrepare()
    {
        return IsPrepared(Player2Show) &&
               IsPrepared(Player3Show) &&
               IsPrepared(Player4Show);
    }

    private bool IsPrepared(PlayerShow show)
    {
        if (show == null ||
            !show.gameObject.activeSelf ||
            show.nameText == null ||
            string.IsNullOrEmpty(show.nameText.text))
        {
            return true;
        }

        return show.IsPrepare;
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

            if (HostShow != null)
                HostShow.transform.SetAsFirstSibling();
        }
        else
        {
            HidePlayer(HostShow);
        }

        if (players == null)
        {
            SetPlayerShowOrder();
            return;
        }

        UpdatePlayerSlot(Player2Show, players);
        UpdatePlayerSlot(Player3Show, players);
        UpdatePlayerSlot(Player4Show, players);

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i] == null ||
                string.IsNullOrEmpty(players[i].Name) ||
                Contain2(players[i].Name))
            {
                continue;
            }

            if (Host != null &&
                players[i].Name == Host.Name)
            {
                continue;
            }

            AssignPlayerToFreeSlot(players[i].Name);
        }

        SetPlayerShowOrder();
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
            !Contain(players, show.nameText.text))
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
        return IsPlayer(Player2Show, name) ||
               IsPlayer(Player3Show, name) ||
               IsPlayer(Player4Show, name);
    }

    private void ClearPreview(PlayerShow show)
    {
        if (show == null)
            return;

        if (show.plantInGrid != null)
        {
            show.plantInGrid.Dead(
                false,
                0f,
                true,
                false
            );

            show.plantInGrid = null;
        }

        if (show.zombieInGrid != null)
        {
            show.zombieInGrid.DirectDead(
                false,
                0f,
                true
            );

            show.zombieInGrid = null;
        }

        HideGridSelector(show);
    }
}