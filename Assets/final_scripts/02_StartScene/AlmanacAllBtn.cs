using UnityEngine;

namespace StartScene
{
    public class AlmanacAllBtn : MonoBehaviour
    {
        public int BtnType;

        public Renderer REnderer;

        private void OnMouseEnter()
        {
            if (!MyTool.IsPointerOverGameObject())
            {
                if (REnderer != null)
                {
                    REnderer.material.SetFloat("_Brightness", 1.3f);
                }

                if (AudioManager.Instance != null && GameManager.Instance != null)
                {
                    AudioManager.Instance.PlayEFAudio(
                        GameManager.Instance.AudioConf.Bleep,
                        transform.position,
                        isAll: true
                    );
                }
            }
        }

        private void OnMouseExit()
        {
            if (REnderer != null)
            {
                REnderer.material.SetFloat("_Brightness", 1f);
            }
        }

        private void OnMouseDown()
        {
            if (MyTool.IsPointerOverGameObject())
            {
                return;
            }

            if (AudioManager.Instance != null && GameManager.Instance != null)
            {
                if (BtnType != 4 && BtnType != 5 && BtnType != 6)
                {
                    if (Random.Range(0, 2) == 1)
                    {
                        AudioManager.Instance.PlayEFAudio(
                            GameManager.Instance.AudioConf.Tap,
                            transform.position,
                            isAll: true
                        );
                    }
                    else
                    {
                        AudioManager.Instance.PlayEFAudio(
                            GameManager.Instance.AudioConf.Tap2,
                            transform.position,
                            isAll: true
                        );
                    }
                }
                else
                {
                    AudioManager.Instance.PlayEFAudio(
                        GameManager.Instance.AudioConf.GraveButton,
                        transform.position,
                        isAll: true
                    );
                }
            }

            if (AlmanacScence.Instance == null)
            {
                return;
            }

            if (BtnType == 0)
            {
                if (CameraControl.Instance != null)
                {
                    CameraControl.Instance.SetPosition(
                        new Vector2(0f, -30f)
                    );
                }
            }
            else if (BtnType == 1)
            {
                AlmanacScence.Instance.OpenPlantPage();
            }
            else if (BtnType == 2)
            {
                AlmanacScence.Instance.OpenMapPage();
            }
            else if (BtnType == 3)
            {
                AlmanacScence.Instance.OpenCmdPage();
            }
            else if (BtnType == 4)
            {
                AlmanacScence.Instance.OpenZombiePage();
            }
            else if (BtnType == 5)
            {
                AlmanacScence.Instance.OpenWeatherPage();
            }
            else if (BtnType == 6)
            {
                AlmanacScence.Instance.OpenOtherPage();
            }
            else if (BtnType == 7)
            {
                AlmanacScence.Instance.BackMenu();
            }
            else if (BtnType == 8)
            {
                AlmanacScence.Instance.NextPage();
            }
        }
    }
}