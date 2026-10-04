using UnityEngine;

public class MultiplayerButton : MonoBehaviour
{
    public Renderer REnderer;

    private void OnMouseEnter()
    {
        if (MyTool.IsPointerOverGameObject())
            return;

        REnderer.material.SetFloat(
            "_Brightness",
            1.3f
        );

        AudioManager.Instance.PlayEFAudio(
            GameManager.Instance.AudioConf.Bleep,
            transform.position,
            isAll: true
        );
    }

    private void OnMouseExit()
    {
        REnderer.material.SetFloat(
            "_Brightness",
            1f
        );
    }

    private void OnMouseDown()
    {
        if (MyTool.IsPointerOverGameObject())
            return;

        CameraControl.Instance.MoveTo(
            new Vector2(-21.3f, -30f),
            null
        );

        AudioManager.Instance.PlayEFAudio(
            GameManager.Instance.AudioConf.GraveButton,
            transform.position,
            isAll: true
        );
    }
}