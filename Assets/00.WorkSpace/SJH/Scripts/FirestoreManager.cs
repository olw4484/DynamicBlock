using Firebase;
using Firebase.Extensions;
using Firebase.Firestore;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using System.Threading.Tasks;
using System;

public class FirestoreManager : MonoBehaviour
{
	public static FirestoreManager Instance { get; private set; }
	public static FirebaseFirestore Firestore { get; private set; }

	//Test
	[SerializeField] private string _stageName;
	[SerializeField] private bool _isClear;
	[SerializeField] private Button _readBtn;
	[SerializeField] private Button _writeBtn;
	[SerializeField] private Button _isClearBtn;
	[SerializeField] private TMP_Text _isClearText;
	[SerializeField] private TMP_Text _resultText;

	// StageData["StageName"][StageIndex]["Success" or "Failed"] = 스테이지별 첫시도시 성공 확률
	public Dictionary<string, Dictionary<long, Dictionary<string, long>>> StageData = new Dictionary<string, Dictionary<long, Dictionary<string, long>>>();

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
			WriteStageData(_stageName, _isClear);
		});
		_isClearText.text = $"{_isClear}";
		_isClearBtn.onClick.AddListener(() =>
		{
			_resultText.text = "IsClear Button Click";
			_isClear = !_isClear;
			_isClearText.text = $"{_isClear}";
		});
	}

	public void WriteStageData(string stageName, bool isClear)
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
		// 포인터
		DocumentReference stageRef = Firestore.Collection("StageData").Document("Stage1").Collection("Stages").Document(stageName);

		string clearKey = isClear ? "Success" : "Failed";

		var data = new Dictionary<string, object>()
		{
			{ clearKey, FieldValue.Increment(1) }
		};
		stageRef.UpdateAsync(data).ContinueWithOnMainThread(task =>
		{
			if (task.IsCompletedSuccessfully)
			{
				Debug.Log("업데이트 성공");
				_resultText.text = "업데이트 성공";
				return;
			}
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
	}

	public void StageDataToJson()
	{
		StageData.Clear();

		// 포인터
		Query stageData = Firestore.CollectionGroup("Stages"); // Stages 라는 컬렉션 그룹 전부 캐싱

		stageData.GetSnapshotAsync(Source.Server).ContinueWithOnMainThread(task =>
		{
			if (task.IsFaulted || task.IsCanceled) return;

			QuerySnapshot rootQs = task.Result;
			Debug.Log($"Stages를 포함한 문서 수 : {rootQs.Count}");
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
					float rate = (float)s / t;
					Debug.Log($"[{stageName}-{stageIndex}] 첫 시도시 성공 확률 : {rate:P0}");
				}
			}
		});
	}
}
