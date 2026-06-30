using UnityEngine;

public class DrawerInteract : MonoBehaviour
{
    public Transform drawerMesh;
    public Transform closedPos;
    public Transform openPos;

    public float speed = 8f;

    private bool isOpen = false;
    private bool playerInRange = false;

    void Update()
    {
        // 按E切换开关
        if (playerInRange && Input.GetKeyDown(KeyCode.E))
        {
            isOpen = !isOpen;
        }

        // 移动到目标位置
        Transform target = isOpen ? openPos : closedPos;

        drawerMesh.position = Vector3.Lerp(
            drawerMesh.position,
            target.position,
            Time.deltaTime * speed
        );
    }

    // 进入交互范围
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
        }
    }

    // 离开交互范围
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
        }
    }
}