using System;
using System.IO;
using UnityEngine;
using SJH;

namespace SJH
{
	[Serializable]
	public class GameData
	{
		public int LanguageIndex;
	}
}

public class SaveLoadManager : MonoBehaviour
{
	public static SaveLoadManager Instance { get; private set; }

	private const string _saveFileName = "SaveData.json";
#if UNITY_EDITOR
	public string DataPath => Path.Combine(Application.dataPath, $"00.WorkSpace/SJH/SaveFile/{_saveFileName}");
#else
	public string DataPath => Path.Combine(Application.persistentDataPath, $"SaveFile/{_saveFileName}");
#endif
	public SJH.GameData GameData;

	void Awake()
	{
		Instance = this;
		// TODO : 로드
		//LoadData();
	}

	void OnApplicationQuit()
	{
		// TODO : 세이브
		SaveData();
	}

	public bool LoadData()
	{
		Debug.Log("데이터 로드");
		if (!ExistData())
		{
			GameData = new SJH.GameData();
			GameData.LanguageIndex = 0;
			return false;
		}

		string json = File.ReadAllText($"{DataPath}");
		GameData = JsonUtility.FromJson<SJH.GameData>(json);
		return true;
	}
	public void SaveData()
	{
		Debug.Log("데이터 세이브");

		// 폴더 없으면 생성
		string dir = Path.GetDirectoryName($"{DataPath}");
		if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

		string json = JsonUtility.ToJson(GameData, true);
		File.WriteAllText($"{DataPath}", json);
	}

	public bool ExistData()
	{
		// 저장 경로 유무 체크
		string dir = Path.GetDirectoryName($"{DataPath}");
		if (!Directory.Exists(dir)) return false;
		return File.Exists($"{DataPath}");
	}
}
