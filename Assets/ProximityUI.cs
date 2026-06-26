using UnityEngine;

public class ProximityUI : MonoBehaviour
{
    [Header("检测设置")]
    public float detectionRadius = 5f;          // 触发距离
    public string playerTag = "Player";         // 玩家标签

    [Header("UI 引用")]
    public GameObject uiObject;                 // 要显示/隐藏的 UI（World Space Canvas）

    private Transform player;
    private Camera mainCamera;

    void Start()
    {
        // 1. 获取玩家
        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj != null)
            player = playerObj.transform;
        else
            Debug.LogWarning("未找到玩家！请确保玩家对象带有 'Player' 标签。");

        // 2. 获取主摄像机
        mainCamera = Camera.main;
        if (mainCamera == null)
            Debug.LogWarning("未找到主摄像机，请检查场景中是否有 Camera 标记为 MainCamera。");

        // 3. 初始隐藏 UI
        if (uiObject != null)
            uiObject.SetActive(false);
        else
            Debug.LogWarning("请将 UI 对象拖入 uiObject 字段。");
    }

    void Update()
    {
        if (player == null || mainCamera == null || uiObject == null)
            return;

        // 计算玩家与当前物体的距离
        float distance = Vector3.Distance(transform.position, player.position);

        if (distance <= detectionRadius)
        {
            // 显示 UI（若未显示）
            if (!uiObject.activeSelf)
                uiObject.SetActive(true);

            // 让 UI 始终面向摄像机（世界空间 Canvas 的正面为 Z 轴正方向）
            Vector3 direction = mainCamera.transform.position - uiObject.transform.position;
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                uiObject.transform.rotation = targetRotation;
            }
        }
        else
        {
            // 隐藏 UI
            if (uiObject.activeSelf)
                uiObject.SetActive(false);
        }
    }
}