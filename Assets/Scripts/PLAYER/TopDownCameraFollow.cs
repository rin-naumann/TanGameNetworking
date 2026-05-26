using UnityEngine;

public class TopDownCameraFollow : MonoBehaviour
{
    [SerializeField] Vector3 offset = new Vector3(0, 10f, -8f);
    [SerializeField] float followSpeed = 10f;
    private Transform target;
    
    public void setTarget(Transform target) => this.target = target;

    private void LateUpdate()
    {
        if (target == null) return;
        Vector3 desiredPos = target.position + offset;
        transform.position = 
            Vector3.Lerp(transform.position, desiredPos, followSpeed * Time.deltaTime);
        transform.LookAt(transform.position);
    }
}
