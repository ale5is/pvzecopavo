using System.Collections.Generic;
using System.IO;
using SaveClass;
using UnityEngine;

public class ChooseSave : MonoBehaviour
{
    public static ChooseSave Instance;

    public GameObject SaveOption;

    public GameObject Content;

    public AddUser AddUser;

    public EditUser EditUser;

    public SaveOption SelectedSaveOption;

    private List<SaveOption> saveOptions =
        new List<SaveOption>();

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (AddUser != null)
            AddUser.gameObject.SetActive(false);

        if (EditUser != null)
            EditUser.gameObject.SetActive(false);

        gameObject.SetActive(false);
    }

    public bool CheckNameRepeat(string Name)
    {
        for (int i = 0; i < saveOptions.Count; i++)
        {
            if (saveOptions[i] != null &&
                saveOptions[i].SaveNameText != null &&
                saveOptions[i].SaveNameText.text == Name)
            {
                return true;
            }
        }

        return false;
    }

    public void ShowEditUser()
    {
        if (SelectedSaveOption == null ||
            EditUser == null)
        {
            return;
        }

        gameObject.SetActive(true);
        EditUser.gameObject.SetActive(true);

        if (EditUser.inputField != null &&
            SelectedSaveOption.userSave != null)
        {
            EditUser.inputField.text =
                SelectedSaveOption.userSave.playerName;
        }
    }

    public void ShowAddUser()
    {
        if (AddUser == null)
            return;

        gameObject.SetActive(true);
        AddUser.Display(true);
    }

    public void ShowFirstAddUser()
    {
        ClearOptions();

        gameObject.SetActive(true);

        if (AddUser != null)
            AddUser.Display(false);
    }

    public void LoadSave(
        UserSave saveUser,
        string path)
    {
        if (GameManager.Instance == null ||
            saveUser == null ||
            string.IsNullOrEmpty(path))
        {
            return;
        }

        GameManager.Instance.LoadSave(
            saveUser,
            path
        );

        Cancel();
    }

    public void Confirm()
    {
        if (SelectedSaveOption == null ||
            SelectedSaveOption.userSave == null ||
            SelectedSaveOption.Path == null)
        {
            return;
        }

        LoadSave(
            SelectedSaveOption.userSave,
            SelectedSaveOption.Path.FullName
        );
    }

    public void Cancel()
    {
        ClearOptions();

        if (AddUser != null)
            AddUser.gameObject.SetActive(false);

        if (EditUser != null)
            EditUser.gameObject.SetActive(false);

        gameObject.SetActive(false);
    }

    private void ClearOptions()
    {
        for (int i = 0; i < saveOptions.Count; i++)
        {
            if (saveOptions[i] != null)
                Destroy(saveOptions[i].gameObject);
        }

        saveOptions.Clear();

        if (Content != null)
        {
            RectTransform rect =
                Content.GetComponent<RectTransform>();

            if (rect != null)
                rect.sizeDelta =
                    new Vector2(0f, 20f);
        }

        SelectedSaveOption = null;
    }

    public void LoadSavegroup()
    {
        ClearOptions();

        if (GameManager.Instance == null ||
            string.IsNullOrEmpty(
                GameManager.Instance.SavePath))
        {
            ShowFirstAddUser();
            return;
        }

        if (!Directory.Exists(
            GameManager.Instance.SavePath))
        {
            Directory.CreateDirectory(
                GameManager.Instance.SavePath
            );

            ShowFirstAddUser();
            return;
        }

        DirectoryInfo[] directories =
            new DirectoryInfo(
                GameManager.Instance.SavePath
            ).GetDirectories();

        for (int i = 0; i < directories.Length; i++)
        {
            string file =
                Path.Combine(
                    directories[i].FullName,
                    FixedInfo.PlayerInfoName
                );

            if (!File.Exists(file))
                continue;

            try
            {
                string json =
                    GameManager.Decrypt(
                        File.ReadAllText(file)
                    );

                UserSave saveUser =
                    JsonUtility.FromJson<UserSave>(
                        json
                    );

                if (saveUser == null)
                    continue;

                if (SaveOption == null ||
                    Content == null)
                {
                    continue;
                }

                SaveOption component =
                    Instantiate(SaveOption)
                        .GetComponent<SaveOption>();

                if (component == null)
                    continue;

                saveOptions.Add(component);

                component.GetSaveinfo(
                    saveUser,
                    directories[i]
                );

                component.transform.SetParent(
                    Content.transform,
                    false
                );

                component.gameObject.SetActive(true);
            }
            catch
            {
                continue;
            }
        }

        if (Content != null)
        {
            RectTransform rect =
                Content.GetComponent<RectTransform>();

            if (rect != null)
            {
                rect.sizeDelta =
                    new Vector2(
                        0f,
                        45f * saveOptions.Count
                    );
            }
        }

        if (saveOptions.Count == 0)
        {
            ShowFirstAddUser();
            return;
        }

        bool foundCurrentPlayer = false;

        if (GameManager.Instance.LocalPlayerSave != null)
        {
            for (int i = 0; i < saveOptions.Count; i++)
            {
                if (saveOptions[i] == null ||
                    saveOptions[i].userSave == null)
                {
                    continue;
                }

                if (saveOptions[i].userSave.playerName ==
                    GameManager.Instance.LocalPlayerSave.playerName)
                {
                    foundCurrentPlayer = true;

                    SelectedSaveOption =
                        saveOptions[i];

                    SelectedSaveOption.OnPointerClick(null);

                    SelectedSaveOption.transform.SetSiblingIndex(0);

                    break;
                }
            }
        }

        if (!foundCurrentPlayer)
        {
            SelectedSaveOption =
                saveOptions[0];

            SelectedSaveOption.OnPointerClick(null);

            SelectedSaveOption.transform.SetSiblingIndex(0);

            LoadSave(
                SelectedSaveOption.userSave,
                SelectedSaveOption.Path.FullName
            );
        }
    }

    public void RenewSelect(
        SaveOption saveOption)
    {
        if (saveOption == null)
            return;

        SelectedSaveOption =
            saveOption;

        for (int i = 0; i < saveOptions.Count; i++)
        {
            if (saveOptions[i] != null)
                saveOptions[i].RenewSelect();
        }
    }
}