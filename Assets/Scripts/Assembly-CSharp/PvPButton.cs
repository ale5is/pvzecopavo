using UnityEngine;

public class PvPButton : MonoBehaviour
{
    public Renderer REnderer;

    private Renderer MyRenderer => GetComponent<Renderer>();

    private void OnMouseEnter()
    {
        if (!MyTool.IsPointerOverGameObject() && GameManager.Instance.isOnline)
        {
            REnderer.enabled = true;
            MyRenderer.material.SetFloat("_Brightness", 1.3f);

            AudioManager.Instance.PlayEFAudio(
                GameManager.Instance.AudioConf.Bleep,
                base.transform.position,
                isAll: true
            );
        }
    }

    private void OnMouseExit()
    {
        REnderer.enabled = false;
        MyRenderer.material.SetFloat("_Brightness", 1f);
    }

    private void OnMouseDown()
    {
        if (!MyTool.IsPointerOverGameObject() && GameManager.Instance.isOnline)
        {
            PvPSelector.Instance.OpenAndInit();

            AudioManager.Instance.PlayEFAudio(
                GameManager.Instance.AudioConf.GraveButton,
                base.transform.position,
                isAll: true
            );
        }
    }
}