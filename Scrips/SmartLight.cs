using UnityEngine;
using System.Collections;

public class SmartLight : MonoBehaviour
{
    [SerializeField] private string entityId;
    [SerializeField] private HomeAssistantAPI haApi;
    [SerializeField] private Light[] lightComponents; // 灯光数组
    
    [Header("亮度设置")]
    [SerializeField] private float minIntensity = 0.1f; // 最小亮度
    [SerializeField] private float maxIntensity = 3.0f; // 最大亮度
    [SerializeField] private Material emissiveMaterial; // 可选：发光材质
    
    private bool isOn = false;
    private Color originalEmissionColor;
    private MeshRenderer meshRenderer;
    
    private void Start()
    {
        // 检查组件引用
        if (haApi == null)
        {
            haApi = FindObjectOfType<HomeAssistantAPI>();
            if (haApi == null)
            {
                Debug.LogError($"请为 {gameObject.name} 的SmartLight组件设置HomeAssistantAPI引用");
                return;
            }
        }
        
        // 检查Light组件引用
        if (lightComponents == null || lightComponents.Length == 0)
        {
            // 尝试查找所有子对象中的Light组件
            lightComponents = GetComponentsInChildren<Light>();
            if (lightComponents == null || lightComponents.Length == 0)
            {
                Debug.LogError($"请为 {gameObject.name} 的SmartLight组件设置至少一个Light组件引用");
                return;
            }
        }
        
        // 获取发光材质相关组件
        if (emissiveMaterial != null)
        {
            meshRenderer = GetComponentInChildren<MeshRenderer>();
            if (meshRenderer != null)
            {
                originalEmissionColor = emissiveMaterial.GetColor("_EmissionColor");
            }
        }
    }
    
    public void UpdateState(bool state, float brightness = 1.0f)
    {
        Debug.Log($"灯光 {entityId} 正在更新状态: {state}, 亮度: {brightness}");
        isOn = state;
        
        if (lightComponents != null && lightComponents.Length > 0)
        {
            // 对于switch类型的实体，我们不使用亮度值，直接设置为最大亮度
            float normalizedBrightness = entityId.StartsWith("light.") ? 
                brightness / 255f : 
                1.0f;
            
            float calculatedIntensity = Mathf.Lerp(minIntensity, maxIntensity, normalizedBrightness);
            
            // 更新所有灯光
            foreach (Light light in lightComponents)
            {
                if (light != null)
                {
                    // 确保灯光组件始终启用
                    light.enabled = true;
                    
                    if (isOn)
                    {
                        light.intensity = calculatedIntensity;
                    }
                    else
                    {
                        light.intensity = 0.0f;
                    }
                }
            }
            
            // 更新发光材质
            if (meshRenderer != null && emissiveMaterial != null)
            {
                emissiveMaterial.EnableKeyword("_EMISSION");
                
                if (isOn)
                {
                    emissiveMaterial.SetColor("_EmissionColor", originalEmissionColor * normalizedBrightness * maxIntensity);
                }
                else
                {
                    emissiveMaterial.SetColor("_EmissionColor", Color.black);
                }
            }
            
            Debug.Log($"灯光 {entityId} {(isOn ? "已打开" : "已关闭")}，亮度: {(isOn ? calculatedIntensity : 0)}");
        }
        else
        {
            Debug.LogError($"灯光 {entityId} 缺少Light组件引用");
        }
    }
    
    public string GetEntityId()
    {
        return entityId;
    }
    
    public void ToggleLight()
    {
        if (haApi != null)
        {
            haApi.StartCoroutine(haApi.SetLightState(entityId, !isOn));
        }
        else
        {
            Debug.LogError($"{gameObject.name}的SmartLight组件缺少HomeAssistantAPI引用");
        }
    }
}