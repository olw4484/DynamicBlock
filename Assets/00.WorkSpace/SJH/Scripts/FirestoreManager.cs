using Firebase.Extensions;
using Firebase.Firestore;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 스테이지 데이터 저장 클래스
/// </summary>
public class StageData
{
	/// <summary>
	/// _data["StageName"][StageIndex] = 스테이지별 첫시도시 성공 확률
	/// </summary>
	private Dictionary<string, Dictionary<int, int>> _data;
	/// <summary>
	/// _firstData["StageName"][StageIndex] = 첫 클리어 여부
	/// </summary>
	private Dictionary<string, Dictionary<int, bool>> _firstData;
	/// <summary>
	/// null 확인용
	/// </summary>
	public bool IsNull => _data == null || _data.Count == 0;
	public int Count => IsNull ? 0 : _data.Count;

	public StageData()
	{
		_data = new Dictionary<string, Dictionary<int, int>>();
		_firstData = new Dictionary<string, Dictionary<int, bool>>();
	}
	public void AddRateData(string stageName, int stageIndex, int rate)
	{
		if (_data == null) _data = new Dictionary<string, Dictionary<int, int>>();

		if (!_data.TryGetValue(stageName, out var data))
		{
			data = new Dictionary<int, int>();
			_data[stageName] = data;
		}
		data[stageIndex] = rate;
	}
	/// <summary>
	/// 데이터가 없으면 -1 을 반환, 그 외의 값이 나오면 $"{value}%" 로 사용
	/// </summary>
	/// <param name="stageName"></param>
	/// <param name="stageIndex"></param>
	/// <returns></returns>
	public int GetRateData(int stageIndex, string stageName = "Stage1")
	{
		int result = -1;
		if (this.IsNull) return result;
		if (!_data.TryGetValue(stageName, out var data)) return result;
		if (!data.TryGetValue(stageIndex, out var rate)) return result;
		return rate;
		// -1을 반환하면 인터넷 연결이 되지 않고 캐싱된 데이터가 없으니 데이터가 없다고 출력
	}
	/// <summary>
	/// 로컬에 저장되는 스테이지별 첫 클리어 여부
	/// </summary>
	/// <param name="stageIndex"></param>
	/// <param name="stageName"></param>
	/// <returns></returns>
	public bool IsClear(int stageIndex, string stageName = "Stage1")
	{
		// 데이터 없으면 생성
		if (_firstData == null) _firstData = new Dictionary<string, Dictionary<int, bool>>();
		if (!_firstData.TryGetValue(stageName, out var data))
		{
			data = new Dictionary<int, bool>();
			_firstData[stageName] = data;
		}
		// 데이터가 있고 클리어 했으면 true 반환
		if (data.TryGetValue(stageIndex, out bool result) && result == true) return true;

		// 데이터가 없거나 false면 true로 바꾸고 false로 반환
		data[stageIndex] = true;
		return false;
	}
}

public class FirestoreManager : MonoBehaviour
{
	public static FirestoreManager Instance { get; private set; }
	public FirebaseFirestore Firestore { get; private set; } = null;
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
		Firestore = FirebaseFirestore.GetInstance(AnalyticsManager.Instance.FirebaseApp);

		// 1. 로컬 데이터 로드
		if (!LoadStageData()) StageData = new StageData();
		// 2. 서버 데이터 캐싱
		ServerDataCaching();
	}

	/// <summary>
	/// 어드벤처 모드 스테이지 클리어 시 실행
	/// </summary>
	/// <param name="stageIndex">클리어한 스테이지 번호</param>
	/// <param name="isClear">클리어 여부</param>
	/// <param name="stageName">클리어한 스테이지 이름</param>
	public void WriteStageData(int stageIndex, bool isClear, string stageName = "Stage1")
	{
		Debug.Log("데이터 추가 시도");

		/* DB 계층구조
		StageData
		ㄴ Stage1 -- 현재 스테이지 키값이 필요함
			ㄴSuccess : n
			ㄴFailed : n
		ㄴ Stage2
		ㄴ  ...
		ㄴ  ...
		 */

		if (Firestore == null) return;
		if (string.IsNullOrEmpty(stageName)) return;
		if (stageIndex < 1) return;
		if (StageData == null) StageData = new StageData(); // Init에서 생성해주지만 필요없으면 지우기
		if (StageData.IsClear(stageIndex, stageName)) return; // 첫 시도인지 체크

		SaveStageData();

		// 포인터 컬 > 문 > 컬 > 문 순서
		DocumentReference stageRef = Firestore.Collection("StageData")
									.Document(stageName)
									.Collection("Stages")
									.Document(stageIndex.ToString());

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
				ServerDataCaching();
				return;
			}
			// 업데이트 실패
			// 실패 시 문서 생성 후 다시 업데이트
			var newDoc = new Dictionary<string, object>()
			{
				{ "Success", isClear ? 1 : 0 },
				{ "Failed", isClear ? 0 : 1 },
			};
			stageRef.SetAsync(newDoc, SetOptions.MergeAll).ContinueWithOnMainThread(task2 =>
			{
				if (task2.IsCompletedSuccessfully) ServerDataCaching();
				else SaveStageData();
			});
		});
	}

	public void ServerDataCaching()
	{
		if (Firestore == null) return;

		var temp = new StageData();
		int count = 0;
		// 포인터
		Query stageData = Firestore.CollectionGroup("Stages"); // Stages 컬렉션 그룹 전부 캐싱

		stageData.GetSnapshotAsync(Source.Server).ContinueWithOnMainThread(task =>
		{
			// 서버 캐싱 실패
			if (task.IsFaulted || task.IsCanceled) return;

			QuerySnapshot rootQs = task.Result;

			//Debug.Log($"Stages를 포함한 문서 수 : {rootQs.Count}");

			if (rootQs == null || rootQs.Count < 1) return;

			// StageData/StageName/Stages/StageIndex
			foreach (DocumentSnapshot ds in rootQs.Documents) // Stages 컬렉션 안의 문서들 순회 ex) 1, 2, 3
			{
				if (!ds.Exists) continue;

				DocumentReference docRef = ds.Reference;

				// Stages 예외처리
				CollectionReference stagesCol = docRef.Parent;
				if (stagesCol == null || stagesCol.Id != "Stages") continue;

				// StageName 예외처리
				DocumentReference stageNameDoc = stagesCol.Parent;
				if (stageNameDoc == null || string.IsNullOrEmpty(stageNameDoc.Id)) continue;
				string stageName = stageNameDoc.Id;

				// StageData 예외처리
				CollectionReference stageDataCol = stageNameDoc.Parent;
				if (stageDataCol == null || stageDataCol.Id != "StageData") continue;

				// StageIndex 예외처리
				if (!int.TryParse(docRef.Id, out int stageIndex)) continue;

				Debug.Log($"{docRef.Parent.Parent.Parent.Id}/{stageName}/{docRef.Parent.Id}/{stageIndex}"); // 경로

				if (ds.TryGetValue<long>("Success", out long s) && ds.TryGetValue<long>("Failed", out long f))
				{
					//Debug.Log($"성공 횟수 : {s}");
					//Debug.Log($"실패 횟수 : {f}");
					long t = s + f;

					// 1. int로 형변환
					int rate = (t > 0) ? Mathf.RoundToInt((float)s / (float)t * 100f) : 0; // 0/0 예외처리
					temp.AddRateData(stageName, stageIndex, rate);
					count++;
					Debug.Log($"[{stageName}-{stageIndex}] 첫 시도시 성공 확률 : {rate}%");

					// 2. 문자열 보간
					//Debug.Log($"[{stageName}-{stageIndex}] 첫 시도시 성공 확률 : {r:P0}");
				}
			}

			if (!temp.IsNull)
			{
				// 데이터 저장
				StageData = temp;
				SaveStageData();
				Debug.Log($"총 {count}개의 스테이지 데이터 캐싱 완료 : {temp.Count}");
			}
		});
	}

	public void SaveStageData()
	{
		Debug.Log("스테이지 데이터 세이브 시도");
		if (StageData == null || StageData.IsNull) return;

		// 폴더 없으면 생성
		string dir = Path.GetDirectoryName($"{DataPath}");
		if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

		string json = JsonConvert.SerializeObject(StageData, Formatting.Indented);
		File.WriteAllText($"{DataPath}", json);
		Debug.Log("스테이지 데이터 세이브 성공");
	}
	public bool LoadStageData()
	{
		Debug.Log("스테이지 데이터 로드 시도");
		string dir = Path.GetDirectoryName($"{DataPath}");
		if (!Directory.Exists(dir) || !File.Exists(DataPath))
		{
			Debug.LogWarning("스테이지 데이터 로드 실패");
			return false;
		}

		string json = File.ReadAllText($"{DataPath}");
		StageData loadedData = JsonConvert.DeserializeObject<StageData>(json);
		if (loadedData == null || loadedData.IsNull)
		{
			Debug.LogWarning("스테이지 데이터 로드 실패");
			return false;
		}

		StageData = loadedData;
		Debug.Log("스테이지 데이터 로드 성공");
		return true;
	}

	void OnApplicationQuit()
	{
		SaveStageData();
	}
}
