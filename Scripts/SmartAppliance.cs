using UnityEngine;

public class SmartAppliance : MonoBehaviour
{
    [SerializeField] private string entityId;  // 添加entityId字段
    [SerializeField] private HomeAssistantAPI haApi;  // 添加API引用
    
    public bool isOn = false;
    public GameObject windEffect; // 风流特效对象
    
    [SerializeField] private bool isAirConditioner = false; // 是否为空调
    [SerializeField] private bool isHeatingMode = false; // 是否为制热模式
    
    // 空调模式颜色
    [SerializeField] private Color coolingColor = new Color(0.7f, 0.9f, 1.0f, 0.5f); // 淡蓝色
    [SerializeField] private Color heatingColor = new Color(1.0f, 0.7f, 0.7f, 0.5f); // 淡红色

    // 添加获取entityId的方法
    public string GetEntityId()
    {
        return entityId;
    }

    // 添加供HomeAssistant调用的更新状态方法
    public void UpdateState(bool state)
    {
        isOn = state;
        if (isOn)
        {
            TurnOn();
        }
        else
        {
            TurnOff();
        }
    }
    
    // 设置空调模式（制热/制冷）
    public void SetHeatingMode(bool heating)
    {
        isHeatingMode = heating;
        if (isOn && windEffect != null)
        {
            UpdateWindEffectColor();
        }
    }

    // 修改ToggleAppliance方法，使用HomeAssistant API
    public void ToggleAppliance()
    {
        if (haApi != null)
        {
            // 根据实体ID前缀判断设备类型并调用相应的API
            if (entityId.StartsWith("humidifier."))
            {
                haApi.StartCoroutine(haApi.SetHumidifierState(entityId, !isOn));
            }
            else
            {
                haApi.StartCoroutine(haApi.SetApplianceState(entityId, !isOn));
            }
        }
        else
        {
            Debug.LogError($"{gameObject.name}的SmartAppliance组件缺少HomeAssistantAPI引用");
            // 本地测试用
            isOn = !isOn;
            if (isOn)
            {
                TurnOn();
            }
            else
            {
                TurnOff();
            }
        }
    }

    private void TurnOn()
    {
        Debug.Log($"设备 {entityId} 已开启!");
        // 移除了控制颜色的代码行
        if (windEffect != null)
        {
            Debug.Log($"激活WindEffect: {windEffect.name}");
            windEffect.SetActive(true);
            
            // 如果是空调，根据模式设置风效果颜色
            if (isAirConditioner)
            {
                Debug.Log($"更新空调风效果颜色，制热模式: {isHeatingMode}");
                UpdateWindEffectColor();
            }
        }
        else
        {
            Debug.LogWarning($"设备 {entityId} 的WindEffect为空!");
        }
    }

    private void TurnOff()
    {
        Debug.Log($"设备 {entityId} 已关闭!");
        // 移除了控制颜色的代码行
        if (windEffect != null)
        {
            windEffect.SetActive(false);
        }
    }
    
    // 更新风效果颜色
    private void UpdateWindEffectColor()
    {
        if (windEffect == null) return;
        
        Debug.Log($"开始更新风效果颜色，制热模式: {isHeatingMode}, 目标颜色: {(isHeatingMode ? "红色" : "蓝色")}");
        
        TrailRenderer trailRenderer = windEffect.GetComponent<TrailRenderer>();
        if (trailRenderer != null)
        {
            Color targetColor = isHeatingMode ? heatingColor : coolingColor;
            trailRenderer.startColor = targetColor;
            trailRenderer.endColor = targetColor;
            Debug.Log($"已更新TrailRenderer颜色 - 空调 {entityId} 设置为{(isHeatingMode ? "制热" : "制冷")}模式，颜色已更新为{targetColor}");
        }
        else
        {
            ParticleSystem particleSystem = windEffect.GetComponent<ParticleSystem>();
            if (particleSystem != null)
            {
                Color targetColor = isHeatingMode ? heatingColor : coolingColor;
                
                // 更新主颜色
                var main = particleSystem.main;
                main.startColor = targetColor;
                
                // 更新拖尾颜色
                var trails = particleSystem.trails;
                if (trails.enabled)
                {
                    trails.colorOverLifetime = new ParticleSystem.MinMaxGradient(targetColor);
                    trails.colorOverTrail = new ParticleSystem.MinMaxGradient(targetColor);
                    Debug.Log($"已更新拖尾颜色 - 空调 {entityId}");
                }
                
                // 更新生命周期内颜色
                var colorOverLifetime = particleSystem.colorOverLifetime;
                if (colorOverLifetime.enabled)
                {
                    // 创建一个新的渐变，从目标颜色到半透明的目标颜色
                    Gradient gradient = new Gradient();
                    gradient.SetKeys(
                        new GradientColorKey[] { new GradientColorKey(targetColor, 0.0f), new GradientColorKey(targetColor, 1.0f) },
                        new GradientAlphaKey[] { new GradientAlphaKey(targetColor.a, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
                    );
                    
                    colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
                    Debug.Log($"已更新生命周期内颜色 - 空调 {entityId}");
                }
                
                Debug.Log($"已更新ParticleSystem颜色 - 空调 {entityId} 设置为{(isHeatingMode ? "制热" : "制冷")}模式，颜色已更新为{targetColor}");
            }
            else
            {
                // 检查是否使用了其他类型的渲染器
                Renderer renderer = windEffect.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Color targetColor = isHeatingMode ? heatingColor : coolingColor;
                    renderer.material.color = targetColor;
                    Debug.Log($"已更新Renderer材质颜色 - 空调 {entityId} 设置为{(isHeatingMode ? "制热" : "制冷")}模式，颜色已更新为{targetColor}");
                }
                else
                {
                    Debug.LogWarning($"无法找到WindEffect上的渲染组件，无法更新颜色！请检查WindEffect对象的组件配置。");
                }
            }
        }
    }
}