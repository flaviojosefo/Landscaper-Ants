using UnityEngine;

public class CameraController : MonoBehaviour {

    [SerializeField] private Transform target;

    private Camera cam;
    private Vector3 lastViewportPos;

    private void Start() => cam = GetComponent<Camera>();

    private void Update() {

        Vector3 mouseViewportPos = cam.ScreenToViewportPoint(Input.mousePosition);

        if (Input.GetMouseButtonDown(0))
            lastViewportPos = mouseViewportPos;

        if (Input.GetMouseButton(0)) {

            Vector3 direction = lastViewportPos - mouseViewportPos;

            cam.transform.RotateAround(target.position, Vector3.up, -direction.x * 180f);
            cam.transform.RotateAround(target.position, cam.transform.right, direction.y * 180f);

            lastViewportPos = mouseViewportPos;
        }
    }
}
