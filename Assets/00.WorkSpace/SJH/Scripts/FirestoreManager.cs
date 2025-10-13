using Firebase.Extensions;
using Firebase.Firestore;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
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

    public StageData StageData = null;

    private const string _saveFileName = "StageData.json";
#if UNITY_EDITOR
    public string DataPath => Path.Combine(Application.dataPath, $"00.WorkSpace/SJH/SaveFile/{_saveFileName}");
#else
    public string DataPath => Path.Combine(Application.persistentDataPath, $"SaveFile/{_saveFileName}");
#endif

    static bool _inited;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public static FirestoreManager GetOrCreate()
    {
        if (Instance) return Instance;
        var go = new GameObject(nameof(FirestoreManager));
        var mgr = go.AddComponent<FirestoreManager>();
        return mgr;
    }

    public static void EnsureInitialized()
    {
        GetOrCreate().Init();
    }

    public async void Init()
    {
        if (_inited) return;
        _inited = true;

        Firestore = FirebaseFirestore.GetInstance(AnalyticsManager.Instance.FirebaseApp);

        var s = Firestore.Settings;
        s.PersistenceEnabled = false;

        // 로컬 로드(백그라운드)
        bool ok = await LoadStageDataAsync();
        if (!ok) StageData = new StageData();

        ServerDataCaching();
    }

    // ====== 핵심: ANR 완화된 캐싱 루틴 ======
    public void ServerDataCaching()
    {
        if (Firestore == null) return;

        var stageData = Firestore.CollectionGroup("Stages");

        stageData.GetSnapshotAsync(Source.Server).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled) return;
            var rootQs = task.Result;
            if (rootQs == null || rootQs.Count < 1) return;

            // 메인 스레드에서 코루틴 시작 OK
            StartCoroutine(Co_ProcessAndApply(rootQs));
        });
    }

    // 메인 스레드에서 결과 반영 + 파일 저장 (await 대신 폴링)
    private IEnumerator Co_ProcessAndApply(QuerySnapshot rootQs)
    {
        // 1) 무거운 가공은 백그라운드로
        var bgTask = Task.Run(() =>
        {
            var temp = new StageData();
            int count = 0;

            foreach (var ds in rootQs.Documents)
            {
                if (!ds.Exists) continue;

                var docRef = ds.Reference;
                var stagesCol = docRef.Parent;
                if (stagesCol == null || stagesCol.Id != "Stages") continue;

                var stageNameDoc = stagesCol.Parent;
                if (stageNameDoc == null || string.IsNullOrEmpty(stageNameDoc.Id)) continue;
                string stageName = stageNameDoc.Id;

                var stageDataCol = stageNameDoc.Parent;
                if (stageDataCol == null || stageDataCol.Id != "StageData") continue;

                if (!int.TryParse(docRef.Id, out int stageIndex)) continue;

                if (ds.TryGetValue<long>("Success", out long s) &&
                    ds.TryGetValue<long>("Failed", out long f))
                {
                    long t = s + f;
                    int rate = (t > 0) ? Mathf.RoundToInt((float)s / (float)t * 100f) : 0;
                    temp.AddRateData(stageName, stageIndex, rate);
                    count++;
                }
            }
            return (temp, count);
        });

        // 2) 완료될 때까지 한 프레임씩 양보
        while (!bgTask.IsCompleted) yield return null;
        if (bgTask.Exception != null) { Debug.LogException(bgTask.Exception); yield break; }

        var (tempData, cnt) = bgTask.Result;
        if (tempData.IsNull) yield break;

        // 3) 메인 스레드에서 참조 교체
        StageData = tempData;

        // 4) 저장도 백그라운드 + 폴링
        var saveTask = SaveStageDataAsync();
        while (!saveTask.IsCompleted) yield return null;
        if (saveTask.Exception != null) Debug.LogException(saveTask.Exception);

        Debug.Log($"총 {cnt}개의 스테이지 데이터 캐싱 완료 : {tempData.Count}");
    }

    public async Task SaveStageDataAsync()
    {
        if (StageData == null || StageData.IsNull) return;

        string dir = Path.GetDirectoryName($"{DataPath}");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        await Task.Run(() =>
        {
            var json = JsonConvert.SerializeObject(StageData, Formatting.Indented);
            File.WriteAllText($"{DataPath}", json);
        });
    }

    public async Task<bool> LoadStageDataAsync()
    {
        string dir = Path.GetDirectoryName($"{DataPath}");
        if (!Directory.Exists(dir) || !File.Exists(DataPath)) return false;

        var loaded = await Task.Run(() =>
        {
            var json = File.ReadAllText($"{DataPath}");
            return JsonConvert.DeserializeObject<StageData>(json);
        });

        if (loaded == null || loaded.IsNull) return false;

        StageData = loaded;
        return true;
    }

    public void SaveStageData()
    {
        if (StageData == null || StageData.IsNull) return;
        string dir = Path.GetDirectoryName($"{DataPath}");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        var json = JsonConvert.SerializeObject(StageData, Formatting.Indented);
        File.WriteAllText($"{DataPath}", json);
    }

    public bool LoadStageData()
    {
        string dir = Path.GetDirectoryName($"{DataPath}");
        if (!Directory.Exists(dir) || !File.Exists(DataPath)) return false;
        var json = File.ReadAllText($"{DataPath}");
        var loaded = JsonConvert.DeserializeObject<StageData>(json);
        if (loaded == null || loaded.IsNull) return false;
        StageData = loaded;
        return true;
    }

    void OnApplicationQuit()
    {
        SaveStageData();
    }
}