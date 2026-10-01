using UnityEngine;

namespace StartScene
{
    public class LeaveGameButton : MonoBehaviour
    {
        private Renderer objectRenderer;

        private void Awake()
        {
            objectRenderer = GetComponent<Renderer>();
        }

        private void OnMouseEnter()
        {
            if (!MyTool.IsPointerOverGameObject() &&
                GameManager.Instance != null &&
                GameManager.Instance.isOnline)
            {
                if (objectRenderer != null)
                    objectRenderer.material.SetFloat("_Brightness", 1.3f);

                AudioManager.Instance.PlayEFAudio(
                    GameManager.Instance.AudioConf.Bleep,
                    base.transform.position,
                    isAll: true);
            }
        }

        private void OnMouseExit()
        {
            if (objectRenderer != null)
                objectRenderer.material.SetFloat("_Brightness", 1f);
        }

        private void OnMouseDown()
        {
            if (!MyTool.IsPointerOverGameObject() &&
                GameManager.Instance != null &&
                GameManager.Instance.isOnline)
            {
                MultiplayerUI.Instance?.LeaveGame();

                AudioManager.Instance.PlayEFAudio(
                    GameManager.Instance.AudioConf.ButtonClick,
                    base.transform.position,
                    isAll: true);
            }
        }
    }
}