using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DoorController : MonoBehaviour
{
    [Header("Home Assistant 设置")]
    [Tooltip("控制门的开关实体ID")]
    [SerializeField] private string doorSwitchEntityId;

    [Header("旋转设置")]
    [Tooltip("选择旋转轴")]
    [SerializeField] private RotationAxis rotationAxis = RotationAxis.Y;
    [Tooltip("旋转角度 (-360 到 +360)")]
    [Range(-360f, 360f)]
    [SerializeField] private float rotationAngle = 90f;
    [Tooltip("旋转速度")]
    [SerializeField] private float rotationSpeed = 2.0f;

    // 枚举定义旋转轴选项
    public enum RotationAxis
    {
        X,
        Y,
        Z
    }

    private HomeAssistantWebSocket haWebSocket;
    private Quaternion closedRotation;
    private Quaternion targetRotation;
    private bool isOpen = false;
    private bool isMoving = false;

    private void Start()
    {
        // 保存初始旋转状态
        closedRotation = transform.rotation;
        
        // 计算目标旋转状态
        Vector3 rotationVector = Vector3.zero;
        switch (rotationAxis)
        {
            case RotationAxis.X:
                rotationVector = new Vector3(rotationAngle, 0, 0);
                break;
            case RotationAxis.Y:
                rotationVector = new Vector3(0, rotationAngle, 0);
                break;
            case RotationAxis.Z:
                rotationVector = new Vector3(0, 0, rotationAngle);
                break;
        }
        targetRotation = closedRotation * Quaternion.Euler(rotationVector);

        // 获取HomeAssistantWebSocket组件
        haWebSocket = FindObjectOfType<HomeAssistantWebSocket>();
        if (haWebSocket == null)
        {
            Debug.LogError("未找到HomeAssistantWebSocket组件，门控制器将无法工作！");
            return;
        }

        // 订阅实体状态变化事件
        haWebSocket.OnEntityStateChanged += OnEntityStateChanged;

        // 请求当前状态
        StartCoroutine(RequestCurrentState());
    }

    private void Update()
    {
        if (isMoving)
        {
            // 平滑旋转到目标位置
            Quaternion targetRot = isOpen ? targetRotation : closedRotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
            
            // 检查是否已经接近目标旋转
            if (Quaternion.Angle(transform.rotation, targetRot) < 0.1f)
            {
                transform.rotation = targetRot;
                isMoving = false;
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

    // 处理实体状态变化
    private void OnEntityStateChanged(string entityId, Dictionary<string, object> stateData)
    {
        if (entityId == doorSwitchEntityId)
        {
            // 从状态数据中获取状态值
            if (stateData.TryGetValue("state", out object stateObj) && stateObj is string state)
            {
                UpdateDoorState(state == "on");
            }
        }
    }

    // 更新门的状态
    private void UpdateDoorState(bool isOpen)
    {
        this.isOpen = isOpen;
        isMoving = true;
        
        Debug.Log($"门状态更新: {(isOpen ? "打开" : "关闭")}");
    }

    // 请求当前状态
    private IEnumerator RequestCurrentState()
    {
        // 等待WebSocket连接建立
        yield return new WaitForSeconds(1f);
        
        // 请求实体当前状态
        haWebSocket.RequestEntityState(doorSwitchEntityId);
    }

    // 手动切换门的状态（可用于测试）
    public void ToggleDoor()
    {
        UpdateDoorState(!isOpen);
    }
}