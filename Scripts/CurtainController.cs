using UnityEngine;
using System;
using System.Collections.Generic;

public class CurtainController : MonoBehaviour
{
    [Header("窗帘设置")]
    [Tooltip("窗帘完全打开时的Y位置（较高位置）")]
    [SerializeField] private float openPositionY = 3.0f;
    
    [Tooltip("窗帘完全关闭时的Y位置（较低位置）")]
    [SerializeField] private float closePositionY = 0.0f;
    
    [Header("动画设置")]
    [Tooltip("窗帘移动的平滑度")]
    [SerializeField] private float smoothTime = 0.5f;
    
    [Tooltip("非线性曲线控制，值越大，运动越不线性")]
    [Range(1f, 5f)]
    [SerializeField] private float nonLinearFactor = 2.0f;
    
    [Header("Home Assistant")]
    [Tooltip("当前窗帘位置值(0-1)，0为打开，1为关闭")]
    [Range(0f, 1f)]
    [SerializeField] private float curtainPosition = 0f;
    
    [Tooltip("Home Assistant中窗帘实体的ID")]
    [SerializeField] private string entityId;
    
    [Tooltip("自动同步Home Assistant状态的间隔（秒）")]
    [SerializeField] private float syncInterval = 1.0f;
    
    // 内部变量
    private Vector3 currentVelocity = Vector3.zero;
    private Vector3 targetPosition;
    private float lastCurtainPosition;
    private float syncTimer = 0f;
    private bool isInitialized = false;
    
    // 引用HomeAssistantWebSocket组件
    private HomeAssistantWebSocket haWebSocket;
    
    // 添加获取实体ID的方法
    public string GetEntityId()
    {
        return entityId;
    }
    
    private void Start()
    {
        // 初始化目标位置
        UpdateTargetPosition();
        lastCurtainPosition = curtainPosition;
        
        // 获取HomeAssistantWebSocket组件
        haWebSocket = FindObjectOfType<HomeAssistantWebSocket>();
        if (haWebSocket == null)
        {
            Debug.LogError("未找到HomeAssistantWebSocket组件，窗帘将无法与Home Assistant同步！");
            return;
        }
        
        // 订阅实体状态变化事件
        haWebSocket.OnEntityStateChanged += OnHAEntityStateChanged;
        
        // 初始化完成后，请求当前状态
        RequestCurrentState();
    }
    
    private void OnDestroy()
    {
        // 取消订阅事件
        if (haWebSocket != null)
        {
            haWebSocket.OnEntityStateChanged -= OnHAEntityStateChanged;
        }
    }
    
    private void Update()
    {
        // 检查窗帘位置是否有变化
        if (curtainPosition != lastCurtainPosition)
        {
            UpdateTargetPosition();
            lastCurtainPosition = curtainPosition;
            
            // 如果已初始化且变化来自本地，则同步到HA
            if (isInitialized && haWebSocket != null && !haWebSocket.IsReceivingState)
            {
                SyncToHomeAssistant();
            }
        }
        
        // 平滑移动窗帘到目标位置
        transform.position = Vector3.SmoothDamp(
            transform.position, 
            targetPosition, 
            ref currentVelocity, 
            smoothTime
        );
        
        // 定期同步状态
        syncTimer += Time.deltaTime;
        if (syncTimer >= syncInterval)
        {
            syncTimer = 0f;
            RequestCurrentState();
        }
    }
    
    // 更新目标位置，应用非线性变换
    private void UpdateTargetPosition()
    {
        // 应用非线性变换到curtainPosition
        float nonLinearPosition = ApplyNonLinearTransform(curtainPosition);
        
        // 计算Y位置
        float targetY = Mathf.Lerp(openPositionY, closePositionY, nonLinearPosition);
        
        // 保持X和Z不变，只改变Y
        targetPosition = new Vector3(
            transform.position.x,
            targetY,
            transform.position.z
        );
    }
    
    // 应用非线性变换，使运动更自然
    private float ApplyNonLinearTransform(float position)
    {
        // 使用幂函数实现非线性效果
        // 当nonLinearFactor = 1时，运动是线性的
        // 当nonLinearFactor > 1时，运动在开始和结束时较慢，中间较快
        return Mathf.Pow(position, nonLinearFactor);
    }
    
    // 公共方法，用于从外部设置窗帘位置（例如从Home Assistant接收数据）
    public void SetCurtainPosition(float position)
    {
        // 确保值在0-1范围内
        curtainPosition = Mathf.Clamp01(position);
    }
    
    // 请求Home Assistant中的当前状态
    private void RequestCurrentState()
    {
        if (haWebSocket != null && haWebSocket.IsConnected)
        {
            haWebSocket.GetEntityState(entityId);
        }
    }
    
    // 同步当前状态到Home Assistant
    private void SyncToHomeAssistant()
    {
        if (haWebSocket != null && haWebSocket.IsConnected)
        {
            haWebSocket.SetEntityState(entityId, curtainPosition.ToString());
        }
    }
    
    // 处理Home Assistant实体状态变化事件
    // 修改参数类型以匹配HomeAssistantWebSocket中的事件定义
    private void OnHAEntityStateChanged(string entityId, Dictionary<string, object> state)
    {
        // 检查是否是我们关注的实体
        if (entityId == this.entityId && state.ContainsKey("state"))
        {
            string stateStr = state["state"].ToString();
            // 尝试解析状态值
            if (float.TryParse(stateStr, out float stateValue))
            {
                // 设置窗帘位置
                SetCurtainPosition(stateValue);
                
                // 标记为已初始化
                isInitialized = true;
            }
            else
            {
                Debug.LogWarning($"无法解析Home Assistant实体 {entityId} 的状态值: {stateStr}");
            }
        }
    }
}