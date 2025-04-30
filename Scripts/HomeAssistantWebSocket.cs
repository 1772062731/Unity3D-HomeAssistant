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
    
    // 添加重连相关参数
    [SerializeField] private float reconnectDelay = 5f; // 重连延迟时间(秒)
    [SerializeField] private int maxReconnectAttempts = 10; // 最大重连次数
    private int reconnectAttempts = 0;
    private bool isReconnecting = false;
    private string wsUrl;
    
    private WebSocket websocket;
    private int messageId = 1;
    
    // 存储所有智能设备的引用
    private Dictionary<string, List<SmartLight>> smartLightGroups = new Dictionary<string, List<SmartLight>>();
    
    // 添加实体状态变化事件
    public event Action<string, Dictionary<string, object>> OnEntityStateChanged;
    
    // 添加连接状态属性
    public bool IsConnected => websocket != null && websocket.State == WebSocketState.Open;
    
    // 添加是否正在接收状态的属性
    public bool IsReceivingState { get; private set; }
    
    // 添加实体状态字典
    private Dictionary<string, Dictionary<string, object>> entityStates = new Dictionary<string, Dictionary<string, object>>();
    
    private void Start()
    {
        // 修正WebSocket URL (支持http和https)
        if (haServerUrl.StartsWith("https:"))
        {
            wsUrl = haServerUrl.Replace("https:", "wss:") + "/api/websocket";
        }
        else
        {
            wsUrl = haServerUrl.Replace("http:", "ws:") + "/api/websocket";
        }
        
        Debug.Log($"正在连接到WebSocket: {wsUrl}");
        
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
            // 连接成功，重置重连计数
            reconnectAttempts = 0;
            isReconnecting = false;
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
            Debug.Log($"WebSocket连接已关闭，关闭代码: {e}");
            // 添加重连逻辑
            if (!isReconnecting && Application.isPlaying)
            {
                StartCoroutine(TryReconnect());
            }
        };
        
        // 连接WebSocket
        yield return websocket.Connect();
    }

    // 添加重连方法
    private IEnumerator TryReconnect()
    {
        if (isReconnecting) yield break;
        
        isReconnecting = true;

        // 添加空值检查
        if (string.IsNullOrEmpty(wsUrl))
        {
            Debug.LogError("WebSocket URL为空，无法重连");
            isReconnecting = false;
            yield break;
        }
        
        while (reconnectAttempts < maxReconnectAttempts && Application.isPlaying)
        {
            Debug.Log($"尝试重新连接WebSocket，第{reconnectAttempts + 1}次尝试...");
            
            // 等待指定的延迟时间
            yield return new WaitForSeconds(reconnectDelay);
            
            // 如果已经连接，则退出重连
            if (websocket != null && websocket.State == WebSocketState.Open)
            {
                isReconnecting = false;
                yield break;
            }
            
            // 关闭旧的连接
            if (websocket != null)
            {
                websocket.Close();
                websocket = null;
            }
            
            // 尝试重新连接
            reconnectAttempts++;
            StartCoroutine(ConnectToWebSocket(wsUrl));
            
            // 等待连接结果
            yield return new WaitForSeconds(2f);
            
            // 如果连接成功，退出重连循环
            if (websocket != null && websocket.State == WebSocketState.Open)
            {
                isReconnecting = false;
                yield break;
            }
        }
        
        if (reconnectAttempts >= maxReconnectAttempts)
        {
            Debug.LogError($"WebSocket重连失败，已达到最大重试次数: {maxReconnectAttempts}");
            isReconnecting = false;
        }
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
    
    // 获取实体状态的方法
    public Dictionary<string, object> GetEntityState(string entityId)
    {
        if (entityStates.TryGetValue(entityId, out var state))
        {
            return state;
        }
        return null;
    }
    
    private void ProcessStateChangedEvent(Dictionary<string, object> eventData)
    {
        try
        {
            if (eventData.ContainsKey("data"))
            {
                var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(eventData["data"].ToString());
                
                if (data != null && data.ContainsKey("entity_id") && data.ContainsKey("new_state"))
                {
                    string entityId = data["entity_id"].ToString();
                    var newState = JsonConvert.DeserializeObject<Dictionary<string, object>>(data["new_state"].ToString());
                    
                    // 更新实体状态缓存
                    if (newState != null)
                    {
                        entityStates[entityId] = newState;
                        
                        // 触发事件
                        OnEntityStateChanged?.Invoke(entityId, newState);
                        
                        // 更新对应的智能设备
                        UpdateSmartDevices(entityId, newState);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"处理状态变化事件时出错: {e.Message}");
        }
    }
    
    // 添加 UpdateSmartDevices 方法
    private void UpdateSmartDevices(string entityId, Dictionary<string, object> state)
    {
        try
        {
            if (state.ContainsKey("state"))
            {
                string stateValue = state["state"].ToString();
                
                // 处理灯光设备
                if (entityId.StartsWith("light.") || entityId.StartsWith("switch."))
                {
                    bool isOn = stateValue == "on";
                    float brightness = 255f; // 默认最大亮度
                    
                    // 获取亮度属性 (仅对light实体有效)
                    if (entityId.StartsWith("light.") && isOn && state.ContainsKey("attributes"))
                    {
                        var attributes = JsonConvert.DeserializeObject<Dictionary<string, object>>(state["attributes"].ToString());
                        if (attributes != null && attributes.ContainsKey("brightness"))
                        {
                            brightness = Convert.ToSingle(attributes["brightness"]);
                        }
                    }
                    
                    // 更新灯光设备
                    if (smartLightGroups.ContainsKey(entityId))
                    {
                        foreach (var light in smartLightGroups[entityId])
                        {
                            if (light != null)
                            {
                                light.UpdateState(isOn, brightness);
                            }
                        }
                        Debug.Log($"更新灯光组 {entityId}: 状态={isOn}, 亮度={brightness}, 灯光数量={smartLightGroups[entityId].Count}");
                    }
                }
                
                // 处理电器设备
                if (entityId.StartsWith("switch.") || entityId.StartsWith("humidifier."))
                {
                    bool isOn = stateValue == "on";
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
                }
                
                // 处理空调设备（climate实体）
                if (entityId.StartsWith("climate."))
                {
                    bool isOn = stateValue != "off"; // climate实体可能有多种状态，不仅仅是on/off
                    bool isHeatingMode = false;
                    
                    // 获取空调模式（制热/制冷）
                    if (state.ContainsKey("attributes"))
                    {
                        var attributes = JsonConvert.DeserializeObject<Dictionary<string, object>>(state["attributes"].ToString());
                        
                        // 直接使用state值判断模式，而不是寻找hvac_mode
                        isHeatingMode = stateValue.Trim().Equals("heat", StringComparison.OrdinalIgnoreCase);
                        
                        // 输出调试信息
                        Debug.Log($"空调状态值: '{stateValue}', 制热模式: {isHeatingMode}");
                        
                        // 如果需要，也可以检查hvac_action
                        if (attributes != null && attributes.ContainsKey("hvac_action"))
                        {
                            string hvacAction = attributes["hvac_action"].ToString();
                            Debug.Log($"空调动作: {hvacAction}");
                        }
                    }
                    
                    Debug.Log($"检测到空调状态变化: {entityId}, 状态: {stateValue}, 制热模式: {isHeatingMode}");
                    
                    // 查找并更新对应的SmartAppliance对象
                    SmartAppliance[] appliances = FindObjectsOfType<SmartAppliance>();
                    foreach (var appliance in appliances)
                    {
                        if (appliance.GetEntityId() == entityId)
                        {
                            Debug.Log($"找到匹配的空调对象: {entityId}");
                            appliance.UpdateState(isOn);
                            
                            // 设置空调模式（制热/制冷）
                            appliance.SetHeatingMode(isHeatingMode);
                        }
                    }
                }

                // 处理窗帘设备
                if (entityId.StartsWith("input_number.curtain_position"))
                {
                    if (float.TryParse(stateValue, out float position))
                    {
                        Debug.Log($"检测到窗帘位置变化: {entityId}, 位置: {position}");
                        
                        // 查找并更新对应的CurtainController对象
                        CurtainController[] curtains = FindObjectsOfType<CurtainController>();
                        foreach (var curtain in curtains)
                        {
                            if (curtain.GetEntityId() == entityId)
                            {
                                Debug.Log($"找到匹配的窗帘对象: {entityId}");
                                curtain.SetCurtainPosition(position);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"更新智能设备时出错: {e.Message}");
        }
    }
    
    // 设置实体状态的方法
public void SetEntityState(string entityId, string value)
{
    if (!IsConnected)
    {
        Debug.LogWarning($"WebSocket未连接，无法设置实体状态: {entityId}");
        return;
    }
    
    try
    {
        // 对于input_number类型的实体，需要使用set_value服务
        if (entityId.StartsWith("input_number."))
        {
            string domain = "input_number";
            string service = "set_value";
            string jsonData = $"{{\"entity_id\": \"{entityId}\", \"value\": {value}}}";
            
            string callServiceMessage = $"{{\"id\": {messageId++}, \"type\": \"call_service\", \"domain\": \"{domain}\", \"service\": \"{service}\", \"service_data\": {jsonData}}}";
            websocket.SendText(callServiceMessage);
            Debug.Log($"发送设置实体状态请求: {entityId} = {value}");
        }
        // 对于开关类型的实体
        else if (entityId.StartsWith("switch.") || entityId.StartsWith("light."))
        {
            bool isOn = value.ToLower() == "true" || value == "1" || value.ToLower() == "on";
            string domain = entityId.Split('.')[0]; // 获取实体类型（switch或light）
            string service = isOn ? "turn_on" : "turn_off";
            string jsonData = $"{{\"entity_id\": \"{entityId}\"}}";
            
            string callServiceMessage = $"{{\"id\": {messageId++}, \"type\": \"call_service\", \"domain\": \"{domain}\", \"service\": \"{service}\", \"service_data\": {jsonData}}}";
            websocket.SendText(callServiceMessage);
            Debug.Log($"发送设置实体状态请求: {entityId} = {(isOn ? "on" : "off")}");
        }
        else
        {
            Debug.LogWarning($"不支持的实体类型: {entityId}");
        }
    }
    catch (Exception e)
    {
        Debug.LogError($"设置实体状态时出错: {e.Message}");
    }
}

    // 处理接收到的实体状态
    private void ProcessEntityState(Dictionary<string, object> stateObj)
    {
        IsReceivingState = true;
        try
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
            // 添加对电器实体的处理
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
                
            }
    
            // 添加对电器实体的处理
            if (entityId.StartsWith("switch.") || entityId.StartsWith("humidifier."))  // 检查是否为开关或除湿机实体
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
            }
        }
        finally
        {
            IsReceivingState = false;
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
