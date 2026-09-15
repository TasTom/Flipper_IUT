using UnityEngine;
using UnityEngine.InputSystem;

public class LeftFlipperController : MonoBehaviour
{
    [Header("Flipper angles")]
    public float restAngle = 0f;
    public float activeAngle = -45f;
    public float flipSpeed = 25f;

    private static  PinballControls controls;
    private float currentAngle;

    void Awake()
    {
        controls = new PinballControls();
    }

    void OnEnable()
    {
        controls.GamePlayPF.LeftFlipper.Enable();
    }

    void OnDisable()
    {
        controls.GamePlayPF.LeftFlipper.Disable();
    }

    void Start()
    {
        currentAngle = restAngle;
    }

    void Update()
    {
        bool pressed = controls.GamePlayPF.LeftFlipper.IsPressed();

        float targetAngle = pressed ? activeAngle : restAngle;

        currentAngle = Mathf.Lerp(
            currentAngle,
            targetAngle,
            Time.deltaTime * flipSpeed
        );

        transform.localRotation = Quaternion.Euler(0f, currentAngle, 0f);
    }
    private static void OnQuit(InputAction.CallbackContext ctx)
    {
        Debug.Log("[FlipperController] QUIT GAME");

        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}
