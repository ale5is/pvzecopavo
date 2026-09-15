using UnityEngine;

namespace StartScene
{
	public class Buttonactive : MonoBehaviour
	{
		public Renderer REnderer;
		public GameObject menu;

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
				menu.gameObject.SetActive(value: true);
				AudioManager.Instance.PlayEFAudio(GameManager.Instance.AudioConf.ButtonClick, base.transform.position, isAll: true);
			}
		}
	}
}
