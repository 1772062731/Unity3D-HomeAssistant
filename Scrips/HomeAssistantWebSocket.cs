using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using NativeWebSocket; // 需要导入WebSocket库
using Newtonsoft.Json; // 需要导入Json.NET库处理复杂JSON

public class HomeAssistantWebSocket : MonoBehaviour
{
    [SerializeField] private string haServerUrl;
    [SerializeField] private string longLivedToken;
    
    private WebSocket websocket;
    private int messageId = 1;
    
    // 存储所有智能设备的引用
    // 存储智能设备，使用List而不是Dictionary
    private Dictionary<string, List<SmartLight>> smartLightGroups = new Dictionary<string, List<SmartLight>>();
    
    private void Start()
    {
        // 替换为WebSocket URL
        string wsUrl = haServerUrl.Replace("http:", "ws:") + "/api/websocket";
        
        // 查找场景中所有SmartLight组件并注册
        RegisterAllSmartDevices();
        
        StartCoroutine(ConnectToWebSocket(wsUrl));
    }
    
    private void RegisterAllSmartDevices()
    {
        // 查找所有SmartLight组件
        SmartLight[] lights = FindObjectsOfType<SmartLight>();
        foreach (var light in lights)
        {
            string entityId = light.GetEntityId();
            
            // 如果这个entityId还没有对应的列表，创建一个新列表
            if (!smartLightGroups.ContainsKey(entityId))
            {
                smartLightGroups[entityId] = new List<SmartLight>();
            }
            
            // 将灯光添加到对应entityId的列表中
            smartLightGroups[entityId].Add(light);
            Debug.Log($"注册设备: {entityId} (总数: {smartLightGroups[entityId].Count})");
        }
        
        // 检查场景中是否有使用switch实体的SmartLight
        foreach (var light in lights)
        {
            string entityId = light.GetEntityId();
            // 如果实体ID以light.开头，检查是否有对应的switch实体
            if (entityId.StartsWith("light."))
            {
                // 尝试查找可能对应的switch实体
                string possibleSwitchId = "switch." + entityId.Substring(6);
                Debug.Log($"检查是否有对应的switch实体: {possibleSwitchId}");
                
                // 也为可能的switch实体创建一个条目
                if (!smartLightGroups.ContainsKey(possibleSwitchId))
                {
                    smartLightGroups[possibleSwitchId] = new List<SmartLight>();
                    smartLightGroups[possibleSwitchId].Add(light);
                    Debug.Log($"为可能的switch实体创建映射: {possibleSwitchId}");
                }
            }
        }
    }
    
    private IEnumerator ConnectToWebSocket(string wsUrl)
    {
        websocket = new WebSocket(wsUrl);
        
        websocket.OnOpen += () => {
            Debug.Log("WebSocket连接已打开");
            // 认证
            SendAuthentication();
        };
        
        websocket.OnMessage += (bytes) => {
            string message = Encoding.UTF8.GetString(bytes);
            Debug.Log("收到WebSocket消息: " + message);
            ProcessMessage(message);
        };
        
        websocket.OnError += (e) => {
            Debug.LogError("WebSocket错误: " + e);
        };
        
        websocket.OnClose += (e) => {
            Debug.Log("WebSocket连接已关闭");
        };
        
        // 连接WebSocket
        yield return websocket.Connect();
    }
    
    private void SendAuthentication()
    {
        string authMessage = $"{{\"type\": \"auth\", \"access_token\": \"{longLivedToken}\"}}";
        websocket.SendText(authMessage);
        Debug.Log("发送认证消息");  // 添加日志
    }
    
    // 订阅状态变化
    private void SubscribeToEvents()
    {
        string subscribeMessage = $"{{\"id\": {messageId++}, \"type\": \"subscribe_events\", \"event_type\": \"state_changed\"}}";
        websocket.SendText(subscribeMessage);
        Debug.Log("订阅状态变化事件");  // 添加日志
    }
    
    // 获取所有实体的当前状态
    private void GetStates()
    {
        string statesMessage = $"{{\"id\": {messageId++}, \"type\": \"get_states\"}}";
        websocket.SendText(statesMessage);
    }
    
    private void ProcessMessage(string message)
    {
        try
        {
            // 添加空检查
            if (string.IsNullOrEmpty(message))
            {
                Debug.LogWarning("收到空消息");
                return;
            }
            
            Dictionary<string, object> msgObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(message);
            
            // 添加空检查
            if (msgObj == null)
            {
                Debug.LogWarning("消息解析失败: " + message);
                return;
            }
            
            if (msgObj.ContainsKey("type"))
            {
                string type = msgObj["type"].ToString();
                
                // 处理认证成功消息
                if (type == "auth_ok")
                {
                    Debug.Log("认证成功");
                    SubscribeToEvents();
                    GetStates(); // 获取所有实体的当前状态
                }
                // 处理事件消息
                else if (type == "event" && msgObj.ContainsKey("event"))
                {
                    var eventData = JsonConvert.DeserializeObject<Dictionary<string, object>>(msgObj["event"].ToString());
                    if (eventData != null && eventData.ContainsKey("event_type") && eventData["event_type"].ToString() == "state_changed")
                    {
                        ProcessStateChangedEvent(eventData);
                    }
                }
                // 处理获取状态的响应
                else if (msgObj.ContainsKey("id") && msgObj.ContainsKey("result"))
                {
                    // 修改空检查的处理方式
                    if (msgObj["result"] == null)
                    {
                        // 这可能是正常情况，改为调试日志而不是警告
                        Debug.Log("收到无结果的响应消息");
                        return;
                    }
                    
                    try
                    {
                        // 检查result是否为数组类型
                        if (msgObj["result"].GetType().ToString().Contains("List") || 
                            msgObj["result"].GetType().ToString().Contains("Array"))
                        {
                            var states = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(msgObj["result"].ToString());
                            if (states != null)
                            {
                                Debug.Log($"成功获取到 {states.Count} 个实体状态");
                                foreach (var state in states)
                                {
                                    // 将UpdateEntityState改为ProcessEntityState
                                    ProcessEntityState(state);
                                }
                            }
                        }
                        else
                        {
                            Debug.Log($"收到非数组类型的结果: {msgObj["result"].GetType()}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"解析状态数据时出错: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"处理消息时出错: {e.Message}\n{e.StackTrace}");
        }
    }
    
    private void ProcessStateChangedEvent(Dictionary<string, object> eventData)
    {
        if (eventData.ContainsKey("data"))
        {
            var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(eventData["data"].ToString());
            if (data != null && data.ContainsKey("entity_id") && data.ContainsKey("new_state"))
            {
                string entityId = data["entity_id"].ToString();
                var newState = JsonConvert.DeserializeObject<Dictionary<string, object>>(data["new_state"].ToString());
                
                // 将UpdateEntityState改为ProcessEntityState
                ProcessEntityState(newState);
            }
        }
    }
    
    private void ProcessEntityState(Dictionary<string, object> stateObj)
    {
        // 添加空检查
        if (stateObj == null || !stateObj.ContainsKey("entity_id") || !stateObj.ContainsKey("state"))
        {
            Debug.LogWarning("状态对象无效");
            return;
        }
        
        // 提取公共变量到方法开始处
        string entityId = stateObj["entity_id"].ToString();
        string state = stateObj["state"].ToString();
        
        // 添加更多日志以便调试
        Debug.Log($"收到实体状态更新: {entityId}, 状态: {state}");
        
        Dictionary<string, object> attributes = null;
        
        if (stateObj.ContainsKey("attributes"))
        {
            attributes = JsonConvert.DeserializeObject<Dictionary<string, object>>(stateObj["attributes"].ToString());
        }

        // 检查是否为灯光实体
        if (entityId.StartsWith("light.") || entityId.StartsWith("switch."))
        {
            bool isOn = state == "on";
            float brightness = 255f; // 默认最大亮度
            
            // 获取亮度属性 (仅对light实体有效)
            if (entityId.StartsWith("light.") && isOn && attributes != null && attributes.ContainsKey("brightness"))
            {
                brightness = Convert.ToSingle(attributes["brightness"]);
            }
            
            // 确保字典中有该实体的条目
            if (!smartLightGroups.ContainsKey(entityId))
            {
                Debug.LogWarning($"未找到实体 {entityId} 的注册灯光，尝试重新查找");
                RegisterAllSmartDevices();
            }
            
            // 再次检查并更新
            if (smartLightGroups.ContainsKey(entityId))
            {
                // 更新所有使用这个entityId的灯光对象
                foreach (var light in smartLightGroups[entityId])
                {
                    if (light != null)
                    {
                        light.UpdateState(isOn, brightness);
                    }
                }
                
                Debug.Log($"更新灯光组 {entityId}: 状态={isOn}, 亮度={brightness}, 灯光数量={smartLightGroups[entityId].Count}");
            }
            else
            {
                Debug.LogError($"找不到实体 {entityId} 的灯光组");
            }
        }
        // 对电器实体的处理
        if (entityId.StartsWith("switch."))  // 检查是否为开关实体
        {
            bool isOn = state == "on";
            Debug.Log($"检测到电器状态变化: {entityId}, 状态: {isOn}");
            
            // 查找并更新对应的SmartAppliance对象
            SmartAppliance[] appliances = FindObjectsOfType<SmartAppliance>();
            foreach (var appliance in appliances)
            {
                if (appliance.GetEntityId() == entityId)
                {
                    Debug.Log($"找到匹配的电器对象: {entityId}");
                    appliance.UpdateState(isOn);
                }
            }
            
            // 查找并更新对应的SmartApplianceOutline对象
            SmartApplianceOutline[] outlines = FindObjectsOfType<SmartApplianceOutline>();
            foreach (var outline in outlines)
            {
                if (outline.GetEntityId() == entityId)
                {
                    Debug.Log($"找到匹配的轮廓对象: {entityId}, 更新状态为: {isOn}");
                    outline.UpdateState(isOn);
                }
            }
        }
    }
    
    private void Update()
    {
        #if !UNITY_WEBGL || UNITY_EDITOR
        if (websocket != null)
        {
            websocket.DispatchMessageQueue();
        }
        #endif
    }
    
    private void OnApplicationQuit()
    {
        if (websocket != null)
        {
            websocket.Close();
        }
    }
}