using System.Collections.Generic;
using UnityEngine;

namespace StartScene
{
    public class SelectMap : MonoBehaviour
    {
        public static SelectMap Instance;

        public TextMesh Text;
        public SpriteRenderer MapSprite;
        public List<MapStoneBase> Stones = new List<MapStoneBase>();
        public MapStoneBase SelectedStone;
        public Transform Lock;

        public int CurrLvSeriesId => SelectedStone == null ? 0 : Stones.IndexOf(SelectedStone) + 1;

        private SpriteRenderer mapRenderer;
        private BoxCollider2D mapCollider;

        private void Awake()
        {
            Instance = this;

            mapRenderer = GetComponent<SpriteRenderer>();
            mapCollider = GetComponent<BoxCollider2D>();
        }

        public void SelectStone(MapStoneBase selectedStone)
        {
            if (selectedStone == null || SelectedStone == selectedStone)
                return;

            SelectedStone = selectedStone;

            for (int i = 0; i < Stones.Count; i++)
            {
                MapStoneBase stone = Stones[i];

                if (stone != null)
                    stone.ClearSelect();
            }

            if (Lock != null)
                Lock.localScale = Vector3.zero;

            if (Text != null)
                Text.transform.localScale = Vector3.one;

            if (mapRenderer != null)
                mapRenderer.enabled = true;

            if (mapCollider != null)
                mapCollider.enabled = true;

            if (MapSprite != null)
            {
                MapSprite.sprite = SelectedStone.MapSprite;
                MapSprite.color = Color.white;
            }

            GameManager gameManager = GameManager.Instance;

            if (gameManager == null)
                return;

            int stoneIndex = Stones.IndexOf(selectedStone);

            if (stoneIndex == 0)
            {
                gameManager.LoadLvInfo(1);
            }
            else if (stoneIndex == 1)
            {
                if (gameManager.LocalPlayerSave != null &&
                    gameManager.LocalPlayerSave.SwampOpen)
                {
                    gameManager.LoadLvInfo(2);
                }
                else
                {
                    if (Text != null)
                        Text.transform.localScale = Vector3.zero;

                    if (Lock != null)
                        Lock.localScale = new Vector3(1.5f, 1.5f, 1f);

                    if (mapRenderer != null)
                        mapRenderer.enabled = false;

                    if (mapCollider != null)
                        mapCollider.enabled = false;

                    if (MapSprite != null)
                        MapSprite.color = new Color32(100, 100, 100, 255);
                }
            }

            if (LevelSelector.Instance != null)
                LevelSelector.Instance.LoadLastLv();
        }

        private void OnMouseEnter()
        {
            if (MyTool.IsPointerOverGameObject())
                return;

            if (Text != null)
                Text.color = Color.white;

            GameManager gameManager = GameManager.Instance;

            if (AudioManager.Instance != null &&
                gameManager != null &&
                gameManager.AudioConf != null)
            {
                AudioManager.Instance.PlayEFAudio(
                    gameManager.AudioConf.Bleep,
                    transform.position,
                    isAll: true
                );
            }
        }

        private void OnMouseExit()
        {
            if (Text != null)
                Text.color = Color.black;
        }

        private void OnMouseDown()
        {
            if (MyTool.IsPointerOverGameObject())
                return;

            if (LevelSelector.Instance != null)
                LevelSelector.Instance.OpenSelector();
        }
    }
}