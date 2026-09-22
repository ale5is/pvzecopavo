using UnityEngine;

namespace StartScene
{
    public class HostButton : MonoBehaviour
    {
        private Renderer REnderer;

        private void Awake()
        {
            REnderer = GetComponent<Renderer>();
        }

        private void OnMouseEnter()
        {
            if (!MyTool.IsPointerOverGameObject() && !GameManager.Instance.isOnline)
            {
                REnderer.material.SetFloat("_Brightness", 1.3f);
                AudioManager.Instance.PlayEFAudio(GameManager.Instance.AudioConf.Bleep, base.transform.position, isAll: true);
            }
        }

        private void OnMouseExit()
        {
            REnderer.material.SetFloat("_Brightness", 1f);
        }

        private void OnMouseDown()
        {
            if (!MyTool.IsPointerOverGameObject() && !GameManager.Instance.isOnline)
            {
                MultiplayerUI.Instance?.OpenHostCanvas();
                AudioManager.Instance.PlayEFAudio(GameManager.Instance.AudioConf.ButtonClick, base.transform.position, isAll: true);
            }
        }
    }
}
