using System.IO;
using SaveClass;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SaveOption : MonoBehaviour, IPointerEnterHandler, IEventSystemHandler, IPointerExitHandler, IPointerClickHandler
{
    public Text SaveNameText;

    public Image BackGround;

    public UserSave userSave;

    public DirectoryInfo Path;

    public void GetSaveinfo(
        UserSave saveUser,
        DirectoryInfo path)
    {
        Path = path;
        userSave = saveUser;

        if (SaveNameText != null &&
            saveUser != null)
        {
            SaveNameText.text =
                saveUser.playerName;
        }
    }

    public void RenewSelect()
    {
        if (BackGround != null)
        {
            BackGround.color =
                new Color32(
                    30,
                    30,
                    44,
                    byte.MaxValue
                );
        }
    }

    public void DeleteSave()
    {
        if (Path == null ||
            !Path.Exists)
        {
            return;
        }

        DirectoryInfo[] directories =
            Path.GetDirectories();

        for (int i = 0; i < directories.Length; i++)
        {
            directories[i].Delete(
                recursive: true
            );
        }

        Path.Delete(
            recursive: true
        );
    }

    public void OnPointerClick(
        PointerEventData eventData)
    {
        if (ChooseSave.Instance == null)
            return;

        ChooseSave.Instance.RenewSelect(this);

        if (BackGround != null)
        {
            BackGround.color =
                new Color32(
                    20,
                    180,
                    15,
                    byte.MaxValue
                );
        }
    }

    public void OnPointerEnter(
        PointerEventData eventData)
    {
        if (SaveNameText != null)
        {
            SaveNameText.color =
                new Color32(
                    byte.MaxValue,
                    byte.MaxValue,
                    byte.MaxValue,
                    byte.MaxValue
                );
        }
    }

    public void OnPointerExit(
        PointerEventData eventData)
    {
        if (SaveNameText != null)
        {
            SaveNameText.color =
                new Color32(
                    250,
                    220,
                    80,
                    byte.MaxValue
                );
        }
    }
}