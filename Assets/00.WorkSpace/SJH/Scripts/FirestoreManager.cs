using Firebase.Extensions;
using Firebase.Firestore;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class StageClearRate
{
	public int Rate;		// 스테이지 첫 시도시 성공 확률
	public long Success;	// 성공
	public long Failed;		// 실패
}

public class FirestoreManager : MonoBehaviour
{
	public static FirebaseFirestore Instance;

	// test
	[SerializeField] private TMP_Text _firestoreText;
	[SerializeField] private FirebaseFirestore _firestore;
	[SerializeField] private Button _initBtn;
	[SerializeField] private TMP_Text _readText;
	[SerializeField] private Button _readBtn;
	[SerializeField] private TMP_Text _writeText;
	[SerializeField] private Button _writeBtn;

	[SerializeField] private bool _isReset = false;
	[Header("스테이지 이름")]
	[SerializeField] private string _stageName;
	[Header("스테이지 클리어 여부")]
	[SerializeField] private bool _isClear = false;

	public void InitBtn()
	{
		_firestore = FirebaseFirestore.DefaultInstance;
		Instance = FirebaseFirestore.DefaultInstance;
		_firestoreText.text = _firestore?.ToString();

		if (_isReset) _firestore.ClearPersistenceAsync(); // 캐시  초기화
	}

	public void ReadBtn()
	{
		ReadData();
	}

	public void WriteBtn()
	{
		WriteData();
	}

	// test

	void Awake()
	{
		//Instance = FirebaseFirestore.DefaultInstance;
	}

	void Update()
	{
		if (Input.GetKeyDown(KeyCode.Q)) WriteData();
		if (Input.GetKeyDown(KeyCode.W)) ReadData();
	}

	void WriteData()
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

		if (Instance == null) return;
		if (string.IsNullOrEmpty(_stageName)) return;
		// 포인터
		DocumentReference stageRef = Instance.Collection("StageData").Document(_stageName);

		string clearKey = _isClear ? "Success" : "Failed";

		var data = new Dictionary<string, object>()
		{
			{ clearKey, FieldValue.Increment(1) }
		};
		stageRef.UpdateAsync(data).ContinueWithOnMainThread(task =>
		{
			if (task.IsCompletedSuccessfully)
			{
				Debug.Log("업데이트 성공");
			}
			else
			{
				Debug.Log("업데이트 실패");
			}
		});
	}

	void ReadData()
	{
		Debug.Log("데이터 읽기 시도");
		if (Instance == null) return;
		// 포인터
		if (string.IsNullOrEmpty(_stageName)) return;
		DocumentReference docRef = Instance.Collection("StageData").Document(_stageName); // Document(string path)

		// Default : 서버 데이터 읽기, 실패시 캐시된 데이터 반환
		// Cache : 캐시된 데이터 반환
		// Server : 서버 데이터 읽기, 실패시 에러 반환
		docRef.GetSnapshotAsync(Source.Cache).ContinueWithOnMainThread(task =>
		{
			DocumentSnapshot snapshot = task.Result;
			
			if (!snapshot.Exists)
			{
				_readText.text = $"{snapshot.Id} 데이터 읽기 실패";
				Debug.Log($"{snapshot.Id} 데이터 읽기 실패");
				return;
			}
			Dictionary<string, object> documentDictionary = snapshot.ToDictionary();
			_readText.text = string.Format("{0} 첫 시도 성공확률 : {1:P0}",
					snapshot.Id,
					((double)(long)documentDictionary["Success"] / ((double)(long)documentDictionary["Success"] + (long)documentDictionary["Failed"]))
					);
			Debug.Log("데이터 읽기 성공");
		});
	}
}
