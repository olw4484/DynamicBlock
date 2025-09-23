using Firebase.Extensions;
using Firebase.Firestore;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json;
using System;

/// <summary>
/// 스테이지 데이터 저장 클래스
/// </summary>
public class StageData
{
	/// <summary>
	/// StageData["StageName"][StageIndex] = 스테이지별 첫시도시 성공 확률
	/// </summary>
	public Dictionary<string, Dictionary<int, int>> Data;
	/// <summary>
	/// null 확인용
	/// </summary>
	public bool IsNull { get => Data.Count == 0; }
	public StageData()
	{
		Data = new Dictionary<string, Dictionary<int, int>>();
	}
	public void Add(string stageName, int stageIndex, int rate)
	{
		if (Data == null) Data = new Dictionary<string, Dictionary<int, int>>();

		if (!Data.TryGetValue(stageName, out var data))
		{
			data = new Dictionary<int, int>();
			Data[stageName] = data;
		}
		data[stageIndex] = rate;
	}
	public int Get(string stageName = "Stage1", int stageIndex = 1)
	{
		int result = -1;
		if (this.IsNull) return result;
		if (!Data.TryGetValue(stageName, out var data)) return result;
		if (!data.TryGetValue(stageIndex, out var rate)) return result;
		return rate;
	}
}

public class FirestoreManager : MonoBehaviour
{
	public static FirestoreManager Instance { get; private set; }
	public FirebaseFirestore Firestore { get; private set; }

	//Test
	[SerializeField] private string _stageName;
	[SerializeField] private int _stageIndex;
	[SerializeField] private bool _isClear;
	[SerializeField] private Button _readBtn;
	[SerializeField] private Button _writeBtn;
	[SerializeField] private Button _isClearBtn;
	[SerializeField] private Button _jsonLoadBtn;
	[SerializeField] private TMP_Text _isClearText;
	[SerializeField] private TMP_Text _resultText;
	//Test

	/// <summary>
	/// StageData["StageName"][StageIndex] = 스테이지별 첫시도시 성공 확률
	/// </summary>
	public StageData StageData = null;

	private const string _saveFileName = "StageData.json";
#if UNITY_EDITOR
	public string DataPath => Path.Combine(Application.dataPath, $"00.WorkSpace/SJH/SaveFile/{_saveFileName}");
#else
	public string DataPath => Path.Combine(Application.persistentDataPath, $"SaveFile/{_saveFileName}");
#endif

	void Awake()
	{
		Instance = this;
	}

	// 애널리틱스에서 초기화
	public void Init()
	{
		_resultText.text = "Firestore Init";
		Firestore = FirebaseFirestore.GetInstance(AnalyticsManager.Instance.FirebaseApp);

		if (Firestore == null)
		{
			Debug.Log("Firestore Init F");
			_resultText.text = "Firestore Init F";
			return;
		}

		#region 테스트 버튼 연결 나중에 삭제
		Debug.Log("Firestore Init S");
		_resultText.text = "Firestore Init S";

		_readBtn.onClick.AddListener(() =>
		{
			_resultText.text = "Read Button Click";
			ReadStageData(_stageName);
		});
		_writeBtn.onClick.AddListener(() =>
		{
			_resultText.text = "Write Button Click";
			WriteStageData(_stageName, _stageIndex, _isClear);
		});
		_isClearText.text = $"{_isClear}";
		_isClearBtn.onClick.AddListener(() =>
		{
			_resultText.text = "IsClear Button Click";
			_isClear = !_isClear;
			_isClearText.text = $"{_isClear}";
		});
		_jsonLoadBtn.onClick.AddListener(() =>
		{
			LoadStageData();
		});
		#endregion
	}

	public void WriteStageData(string stageName, int stageIndex, bool isClear)
	{
		Debug.Log("데이터 추가 시도");
		_resultText.text = "데이터 추가 시도";

		/* DB 계층구조
		StageData
		ㄴ Stage1 -- 현재 스테이지 키값이 필요함
			ㄴSuccess : n
			ㄴFailed : n
		ㄴ Stage2
		ㄴ  ...
		ㄴ  ...
		 */

		if (Instance == null) return;
		if (string.IsNullOrEmpty(stageName)) return;
		if (stageIndex < 1) return;

		// 포인터
		DocumentReference stageRef = Firestore.Collection("StageData").Document(stageName).Collection("Stages").Document(stageIndex.ToString());

		string clearKey = isClear ? "Success" : "Failed";

		var data = new Dictionary<string, object>()
		{
			{ clearKey, FieldValue.Increment(1) }
		};
		stageRef.UpdateAsync(data).ContinueWithOnMainThread(task =>
		{
			// 업데이트 성공
			if (task.IsCompletedSuccessfully)
			{
				Debug.Log("업데이트 성공");
				_resultText.text = "업데이트 성공";
				return;
			}
			// 업데이트 실패
			else
			{
				Debug.Log("업데이트 실패");
				_resultText.text = "업데이트 실패";
				return;
			}
		});
	}

	public void ReadStageData(string stageName)
	{
		Debug.Log("데이터 읽기 시도");
		_resultText.text = "데이터 읽기 시도";
		if (Instance == null) return;
		if (string.IsNullOrEmpty(stageName)) return;
		StageDataToJson();
		return;
		#region 특정 스테이지 캐싱
		// 포인터
		// 컬렉션 > 문서 > 컬렉션 > 문서 순으로 반복되야함
		// 컬렉션 > 컬렉션 X
		DocumentReference stageRef = Firestore.Collection("StageData").Document("Stage1").Collection("Stages").Document(stageName);

		// Default : 서버 데이터 읽기, 실패시 캐시된 데이터 반환
		// Cache : 캐시된 데이터 반환
		// Server : 서버 데이터 읽기, 실패시 에러 반환
		stageRef.GetSnapshotAsync(Source.Default).ContinueWithOnMainThread(task =>
		{
			if (task.IsFaulted || task.IsCanceled)
			{
				_resultText.text = "데이터 읽기 실패";
				return;
			}

			DocumentSnapshot snapshot = task.Result;

			if (!snapshot.Exists)
			{
				_resultText.text = "데이터 읽기 실패";
				return;
			}

			Dictionary<string, object> stageDic = snapshot.ToDictionary();
			if (long.TryParse(stageDic["Success"].ToString(), out long successCount)
			&& long.TryParse(stageDic["Failed"].ToString(), out long failedCount))
			{
				var per = successCount / ((double)(successCount + failedCount));
				Debug.Log($"{snapshot.Id} 첫 시도 성공확률 : {per:P0}");
				_resultText.text = $"{snapshot.Id} 첫 시도 성공확률 : {per:P0}";
				return;
			}
		});
		#endregion
	}

	public void StageDataToJson()
	{
		StageData = new StageData();
		int count = 0;
		// 포인터
		Query stageData = Firestore.CollectionGroup("Stages"); // Stages 컬렉션 그룹 전부 캐싱

		stageData.GetSnapshotAsync(Source.Server).ContinueWithOnMainThread(task =>
		{
			// 서버 캐싱 실패
			if (task.IsFaulted || task.IsCanceled) return;

			QuerySnapshot rootQs = task.Result;

			//Debug.Log($"Stages를 포함한 문서 수 : {rootQs.Count}");

			if (rootQs.Count < 1) return;

			// StageData/StageName/Stages/StageIndex
			foreach (DocumentSnapshot doc in rootQs.Documents) // Stages 컬렉션 안의 문서들 순회 ex) 1, 2, 3
			{
				if (!doc.Exists) continue;

				DocumentReference docRef = doc.Reference;
				// StageData 예외처리
				if (docRef.Parent.Parent.Parent.Id != "StageData") continue;
				// Stages 예외처리
				if (docRef.Parent.Id != "Stages") continue;
				// StageIndex 예외처리
				if (!int.TryParse(docRef.Id, out int stageIndex)) continue;
				string stageName = docRef.Parent.Parent.Id;
				
				Debug.Log($"{docRef.Parent.Parent.Parent.Id}/{stageName}/{docRef.Parent.Id}/{stageIndex}"); // 경로

				if (doc.TryGetValue<int>("Success", out int s) && doc.TryGetValue<int>("Failed", out int f))
				{
					//Debug.Log($"성공 횟수 : {s}");
					//Debug.Log($"실패 횟수 : {f}");
					int t = s + f;
					float r = (float)s / t;
					// 1. int로 형변환
					int rate = Mathf.RoundToInt(r * 100);
					StageData.Add(stageName, stageIndex, rate);
					count++;
					Debug.Log($"[{stageName}-{stageIndex}] 첫 시도시 성공 확률 : {rate}%");

					// 2. 문자열 보간
					//Debug.Log($"[{stageName}-{stageIndex}] 첫 시도시 성공 확률 : {r:P0}");
				}
			}
		Debug.Log($"총 {count}개의 스테이지 데이터 캐싱 완료 : {StageData.Data.Count}");

		// 데이터 저장
		SaveStageData();
		});
	}

	public void SaveStageData()
	{
		Debug.Log("스테이지 데이터 세이브");

		// 폴더 없으면 생성
		string dir = Path.GetDirectoryName($"{DataPath}");
		if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

		if (StageData == null) return;

		string json = JsonConvert.SerializeObject(StageData, Formatting.Indented);
		File.WriteAllText($"{DataPath}", json);
	}
	public bool LoadStageData()
	{
		Debug.Log("스테이지 데이터 로드");
		if (!ExistData()) return false;

		string json = File.ReadAllText($"{DataPath}");
		StageData = JsonConvert.DeserializeObject<StageData>(json);
		return true;
	}
	public bool ExistData()
	{
		// 저장 경로 유무 체크
		string dir = Path.GetDirectoryName($"{DataPath}");
		if (!Directory.Exists(dir)) return false;
		return File.Exists($"{DataPath}");
	}

	void OnApplicationQuit()
	{
		SaveStageData();
	}
}
