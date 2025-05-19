using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GasAlarmLight : MonoBehaviour
{
    [Header("Home Assistant 设置")]
    [Tooltip("烟雾传感器的实体ID")]
    [SerializeField] private string gasSensorEntityId;

    [Header("灯光设置")]
    [Tooltip("闪烁时的点光源")]
    [SerializeField] private Light alarmLight;
    [Tooltip("闪烁频率 (次/秒)")]
    [SerializeField] private float blinkFrequency = 2f;
    [Tooltip("正常状态下的灯光颜色")]
    [SerializeField] private Color normalColor = Color.green;
    [Tooltip("报警状态下的灯光颜色")]
    [SerializeField] private Color alarmColor = Color.red;
    [Tooltip("灯光强度")]
    [SerializeField] private float lightIntensity = 3f;
    [Tooltip("灯光范围")]
    [SerializeField] private float lightRange = 10f;
    
    [Header("可视化设置")]
    [Tooltip("可视化对象（如球体）")]
    [SerializeField] private GameObject visualObject;
    [Tooltip("是否创建默认球体")]
    [SerializeField] private bool createDefaultSphere = true;
    [Tooltip("默认球体大小")]
    [SerializeField] private float defaultSphereSize = 0.5f;
    [Tooltip("报警时球体闪烁的暗色")]
    [SerializeField] private Color alarmDimColor = new Color(0.5f, 0, 0, 1f);

    private HomeAssistantWebSocket haWebSocket;
    private bool isAlarming = false;
    private bool lightOn = true;
    private float blinkTimer = 0f;
    private Renderer visualRenderer;

    private void Start()
    {
        // 初始化点光源
        if (alarmLight == null)
        {
            // 如果没有指定点光源，则创建一个
            GameObject lightObj = new GameObject("AlarmLight");
            lightObj.transform.SetParent(transform);
            lightObj.transform.localPosition = Vector3.zero;
            alarmLight = lightObj.AddComponent<Light>();
            alarmLight.type = LightType.Point;
        }

        // 设置初始灯光属性
        alarmLight.color = normalColor;
        alarmLight.intensity = lightIntensity;
        alarmLight.range = lightRange;
        alarmLight.enabled = true;
        
        // 初始化可视化对象
        InitializeVisualObject();

        // 获取HomeAssistantWebSocket组件
        haWebSocket = FindObjectOfType<HomeAssistantWebSocket>();
        if (haWebSocket == null)
        {
            Debug.LogError("未找到HomeAssistantWebSocket组件，烟雾报警灯将无法工作！");
            return;
        }

        // 订阅实体状态变化事件
        haWebSocket.OnEntityStateChanged += OnEntityStateChanged;

        // 请求当前状态
        StartCoroutine(RequestCurrentState());
    }
    
    private void InitializeVisualObject()
    {
        // 如果没有指定可视化对象且需要创建默认球体
        if (visualObject == null && createDefaultSphere)
        {
            // 创建一个默认的球体
            visualObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visualObject.name = "AlarmSphere";
            visualObject.transform.SetParent(transform);
            visualObject.transform.localPosition = Vector3.zero;
            visualObject.transform.localScale = new Vector3(defaultSphereSize, defaultSphereSize, defaultSphereSize);
            
            // 移除碰撞体（如果不需要的话）
            Collider collider = visualObject.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }
        }
        
        // 获取渲染器组件
        if (visualObject != null)
        {
            visualRenderer = visualObject.GetComponent<Renderer>();
            if (visualRenderer != null)
            {
                // 创建一个新的材质，以便不影响其他使用相同材质的对象
                visualRenderer.material = new Material(visualRenderer.material);
                visualRenderer.material.color = normalColor;
            }
            else
            {
                Debug.LogWarning("可视化对象没有Renderer组件，无法改变颜色！");
            }
        }
    }

    private void OnDestroy()
    {
        // 取消订阅事件
        if (haWebSocket != null)
        {
            haWebSocket.OnEntityStateChanged -= OnEntityStateChanged;
        }
    }

    private void Update()
    {
        // 如果正在报警，则闪烁灯光和可视化对象
        if (isAlarming)
        {
            blinkTimer += Time.deltaTime;
            if (blinkTimer >= 1f / blinkFrequency)
            {
                blinkTimer = 0f;
                lightOn = !lightOn;
                alarmLight.enabled = lightOn;
                
                // 同步更新可视化对象颜色（而不是显示/隐藏）
                if (visualRenderer != null)
                {
                    visualRenderer.material.color = lightOn ? alarmColor : alarmDimColor;
                }
            }
        }
        else
        {
            // 非报警状态，确保灯光常亮
            if (!alarmLight.enabled)
            {
                alarmLight.enabled = true;
            }
            alarmLight.color = normalColor;
            
            // 更新可视化对象为正常状态
            UpdateVisualObjectColor(normalColor);
        }
    }
    
    // 更新可视化对象状态（开/关）- 不再使用此方法
    private void UpdateVisualObjectState(bool isOn)
    {
        if (visualRenderer != null)
        {
            // 不再隐藏对象，只改变颜色
            visualRenderer.material.color = isOn ? alarmColor : alarmDimColor;
        }
    }
    
    // 更新可视化对象颜色
    private void UpdateVisualObjectColor(Color color)
    {
        if (visualRenderer != null)
        {
            visualRenderer.material.color = color;
        }
    }

    // 处理实体状态变化
    private void OnEntityStateChanged(string entityId, Dictionary<string, object> state)
    {
        if (entityId == gasSensorEntityId && state.ContainsKey("state"))
        {
            string stateValue = state["state"].ToString();
            bool isOn = stateValue == "on";
            
            Debug.Log($"烟雾传感器状态变化: {entityId}, 状态: {stateValue}, 报警: {isOn}");
            
            // 更新报警状态
            UpdateAlarmState(isOn);
        }
    }

    // 更新报警状态
    private void UpdateAlarmState(bool isAlarm)
    {
        isAlarming = isAlarm;
        
        if (isAlarming)
        {
            // 报警状态，设置红色
            alarmLight.color = alarmColor;
            UpdateVisualObjectColor(alarmColor);
            Debug.Log("烟雾报警已触发，灯光开始闪烁！");
        }
        else
        {
            // 正常状态，设置绿色
            alarmLight.color = normalColor;
            alarmLight.enabled = true;
            UpdateVisualObjectColor(normalColor);
            Debug.Log("烟雾报警已解除，灯光恢复正常。");
        }
    }

    // 请求当前状态
    private IEnumerator RequestCurrentState()
    {
        // 等待WebSocket连接成功
        yield return new WaitUntil(() => haWebSocket.IsConnected);
        
        // 等待一段时间，确保已经接收到初始状态
        yield return new WaitForSeconds(2f);
        
        // 检查实体状态缓存
        if (haWebSocket.TryGetEntityState(gasSensorEntityId, out Dictionary<string, object> state))
        {
            if (state.ContainsKey("state"))
            {
                string stateValue = state["state"].ToString();
                bool isOn = stateValue == "on";
                
                Debug.Log($"获取到烟雾传感器初始状态: {stateValue}, 报警: {isOn}");
                
                // 更新报警状态
                UpdateAlarmState(isOn);
            }
        }
        else
        {
            Debug.LogWarning($"未能获取到烟雾传感器初始状态: {gasSensorEntityId}");
        }
    }
}