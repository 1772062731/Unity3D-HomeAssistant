using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HomeManager : MonoBehaviour
{
    [SerializeField] private HomeAssistantAPI haApi;
    [SerializeField] private HomeAssistantWebSocket haWebSocket;
    
    // 可以添加UI引用等
    
    private void Start()
    {
        // 确保组件存在
        if (haApi == null)
            haApi = FindObjectOfType<HomeAssistantAPI>();
            
        if (haWebSocket == null)
            haWebSocket = FindObjectOfType<HomeAssistantWebSocket>();
            
        if (haApi == null || haWebSocket == null)
        {
            Debug.LogError("未找到Home Assistant API或WebSocket组件!");
        }
    }
    
    // 可以添加用户交互方法，如点击房间切换视角等
    public void FocusRoom(string roomName)
    {
        // 实现房间聚焦功能
    }
    
    // 可以添加其他管理功能
}