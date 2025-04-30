using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class HomeAssistantAPI : MonoBehaviour
{
    [SerializeField] private string haServerUrl;
    [SerializeField] private string longLivedToken;

    // 获取灯光状态
    public IEnumerator GetLightState(string entityId, Action<bool> callback)
    {
        string url = $"{haServerUrl}/api/states/{entityId}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Authorization", $"Bearer {longLivedToken}");
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string response = request.downloadHandler.text;
                LightStateResponse lightState = JsonUtility.FromJson<LightStateResponse>(response);
                callback(lightState.state == "on");
            }
            else
            {
                Debug.LogError($"Error: {request.error}");
                callback(false);
            }
        }
    }

    // 控制灯光
    public IEnumerator SetLightState(string entityId, bool turnOn, Action<bool> callback = null)
    {
        string url = $"{haServerUrl}/api/services/light/{(turnOn ? "turn_on" : "turn_off")}";
        string jsonData = $"{{\"entity_id\": \"{entityId}\"}}";

        using (UnityWebRequest request = UnityWebRequest.PostWwwForm(url, ""))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", $"Bearer {longLivedToken}");
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                callback?.Invoke(true);
            }
            else
            {
                Debug.LogError($"Error: {request.error}");
                callback?.Invoke(false);
            }
        }
    }
    
    // 控制电器
    public IEnumerator SetApplianceState(string entityId, bool turnOn, Action<bool> callback = null)
    {
        string url = $"{haServerUrl}/api/services/switch/{(turnOn ? "turn_on" : "turn_off")}";
        string jsonData = $"{{\"entity_id\": \"{entityId}\"}}";

        using (UnityWebRequest request = UnityWebRequest.PostWwwForm(url, ""))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", $"Bearer {longLivedToken}");
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                callback?.Invoke(true);
            }
            else
            {
                Debug.LogError($"Error: {request.error}");
                callback?.Invoke(false);
            }
        }
    }

    // 控制除湿机
    public IEnumerator SetHumidifierState(string entityId, bool turnOn, Action<bool> callback = null)
    {
        string url = $"{haServerUrl}/api/services/humidifier/{(turnOn ? "turn_on" : "turn_off")}";
        string jsonData = $"{{\"entity_id\": \"{entityId}\"}}";

        using (UnityWebRequest request = UnityWebRequest.PostWwwForm(url, ""))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", $"Bearer {longLivedToken}");
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                callback?.Invoke(true);
            }
            else
            {
                Debug.LogError($"Error: {request.error}");
                callback?.Invoke(false);
            }
        }
    }
}

[Serializable]
public class LightStateResponse
{
    public string state;
    public Attributes attributes;
}

[Serializable]
public class Attributes
{
    public float brightness;
    // 可以添加更多属性，如颜色、色温等
}

[Serializable]
public class ApplianceStateResponse
{
    public string state;
}