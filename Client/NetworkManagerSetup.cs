using System.IO;
using UnityEngine;

/// <summary>
/// A helper class that registers and initializes the NetworkManager in the Unity scene.
/// Attach this to an empty GameObject to automatically set up the NetworkManager.
/// </summary>
public class NetworkManagerSetup : MonoBehaviour
{
    [Header("Network Settings")]
    public string serverIP = "127.0.0.1";
    public int serverPort = 11000;

    private NetworkManager _networkManager;

    private void Awake()
    {
        LoadConfigFromFile();

        _networkManager = NetworkManager.Instance;

        // Set Ip and Port
        _networkManager.serverIP = serverIP;
        _networkManager.serverPort = serverPort;
    }

    private void Start()
    {
        _networkManager.ConnectToServer();
    }

    private void LoadConfigFromFile()
    {
        string configPath = Path.Combine(Application.dataPath, "Editor", "Debug", "config.txt");

        if (File.Exists(configPath))
        {
            try
            {
                string[] lines = File.ReadAllLines(configPath);
                foreach (string line in lines)
                {
                    if (line.StartsWith("ServerIP="))
                    {
                        string ipValue = line.Substring("ServerIP=".Length).Trim();
                        if (!string.IsNullOrEmpty(ipValue))
                        {
                            serverIP = ipValue;
#if UNITY_EDITOR
                            Debug.Log($"Config: 서버 IP를 {serverIP}로 설정");
#endif
                        }
                    }
                    else if (line.StartsWith("ServerPort="))
                    {
                        string portValue = line.Substring("ServerPort=".Length).Trim();
                        if (int.TryParse(portValue, out int port))
                        {
                            serverPort = port;
#if UNITY_EDITOR
                            Debug.Log($"Config: 서버 포트를 {serverPort}로 설정");
#endif
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"Config 파일 읽기 실패: {ex.Message}. 기본값을 사용합니다.");
#endif
            }
        }
        else
        {
#if UNITY_EDITOR
            Debug.LogWarning($"Config 파일을 찾을 수 없습니다: {configPath}. 기본값을 사용합니다.");
#endif
        }
    }
}
