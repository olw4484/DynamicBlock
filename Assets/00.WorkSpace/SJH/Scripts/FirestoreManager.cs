using Firebase.Extensions;
using Firebase.Firestore;
using System;
using System.Collections.Generic;
using UnityEngine;

public class FirestoreManager : MonoBehaviour
{
	public static FirebaseFirestore Instance;

	void Awake()
	{
		Instance = FirebaseFirestore.DefaultInstance;
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

		CollectionReference usersRef = Instance.Collection("StageData");
		DocumentReference docRef = usersRef.Document("Stage1");
		Dictionary<string, object> user = new Dictionary<string, object>
		{
			{ "Success", UnityEngine.Random.Range(0, 100) },
			{ "Failed", UnityEngine.Random.Range(0, 100) },
		};
		docRef.SetAsync(user).ContinueWithOnMainThread(task =>
		{
			Debug.Log("데이터 추가 성공");
		});
	}

	void ReadData()
	{
		Debug.Log("데이터 읽기 시도");

		CollectionReference usersRef = Instance.Collection("StageData");
		usersRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
		{
			QuerySnapshot snapshot = task.Result;
			foreach (DocumentSnapshot document in snapshot.Documents)
			{
				Dictionary<string, object> documentDictionary = document.ToDictionary();
				Debug.Log(string.Format("{0} 첫 시도 성공확률 : {1:P0}",
					document.Id ,
					((double)(long)documentDictionary["Success"] / ((double)(long)documentDictionary["Success"] + (long)documentDictionary["Failed"]))
					));
			}

			Debug.Log("데이터 읽기 성공");
		});
	}
}
