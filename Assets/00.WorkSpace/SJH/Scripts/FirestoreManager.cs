using Firebase.Extensions;
using Firebase.Firestore;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FirestoreManager : MonoBehaviour
{
	public static FirebaseFirestore Instance;

	//Test
	[SerializeField] private string _stageName;
	[SerializeField] private bool _isClear;
	[SerializeField] private Button _readBtn;
	[SerializeField] private Button _writeBtn;
	[SerializeField] private Button _isClearBtn;
	[SerializeField] private TMP_Text _isClearText;
	[SerializeField] private TMP_Text _resultText;
	//

	void Start()
	{
		_resultText.text = "파이어스토어 초기화 전";
		Instance = FirebaseFirestore.DefaultInstance;
		if (Instance != null)
		{
			Instance.Collection("StageData").GetSnapshotAsync();
			_resultText.text = "파이어스토어 초기화 완료";
		}
		else
		{
			_resultText.text = "파이어스토어 초기화 실패";
		}

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
		DocumentReference stageRef = Instance.Collection("StageData").Document("Stage1").Collection("Stages").Document(stageName);

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

		// 포인터
		// 컬렉션 > 문서 > 컬렉션 > 문서 순으로 반복되야함
		// 컬렉션 > 컬렉션 X
		DocumentReference stageRef = Instance.Collection("StageData").Document("Stage1").Collection("Stages").Document(stageName);

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
}
