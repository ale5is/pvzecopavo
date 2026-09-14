using UnityEngine;

public class ChangeUser : MonoBehaviour
{
    public Sprite Normal;
    public Sprite EnterGreen;
    public Animator clipController;
    public TextMesh NameText;
    public Collider2D Collider;
    public SpriteRenderer SignRe;

    public GameObject ChooseSaveObject;

    public void AnimAction()
    {
        if (Collider != null)
            Collider.enabled = true;
    }

    public void PlayAnimation()
    {
        if (Collider != null)
            Collider.enabled = false;

        if (AudioManager.Instance != null &&
            GameManager.Instance != null &&
            GameManager.Instance.AudioConf != null)
        {
            AudioManager.Instance.PlayEFAudio(
                GameManager.Instance.AudioConf.woodSignRoll_in,
                transform.position,
                isAll: true
            );
        }

        if (clipController != null)
            clipController.Play(
                "anim_drop",
                0,
                0f
            );
    }

    private void OnMouseEnter()
    {
        if (MyTool.IsPointerOverGameObject())
            return;

        if (SignRe != null)
            SignRe.sprite = EnterGreen;

        if (AudioManager.Instance != null &&
            GameManager.Instance != null &&
            GameManager.Instance.AudioConf != null)
        {
            AudioManager.Instance.PlayEFAudio(
                GameManager.Instance.AudioConf.Bleep,
                transform.position,
                isAll: true
            );
        }
    }

    private void OnMouseExit()
    {
        if (SignRe != null)
            SignRe.sprite = Normal;
    }

    private void OnMouseDown()
    {
        if (GameManager.Instance == null)
            return;

        if (GameManager.Instance.isOnline)
            return;

        if (MyTool.IsPointerOverGameObject())
            return;

        ChooseSave chooseSave =
            ChooseSave.Instance;

        if (chooseSave == null)
        {
            Debug.LogError(
                "ChangeUser: ChooseSave.Instance es NULL."
            );

            return;
        }

        Debug.Log(
            "ChangeUser: llamando LoadSavegroup()."
        );

        chooseSave.LoadSavegroup();

        if (AudioManager.Instance != null &&
            GameManager.Instance.AudioConf != null)
        {
            AudioManager.Instance.PlayEFAudio(
                GameManager.Instance.AudioConf.ButtonClick,
                transform.position,
                isAll: true
            );
        }
    }
}