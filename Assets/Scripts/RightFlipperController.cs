using UnityEngine;
using UnityEngine.InputSystem;

public class RightFlipperController : MonoBehaviour
{
    [Header("Flipper angles")]
    public float restAngle = 20f;
    public float activeAngle = -45f;
    public float flipSpeed = 25f;

    private PinballControls controls;
    private float currentAngle;

    void Awake()
    {
        controls = new PinballControls();
	controls.GamePlayPF.Quit.performed += OnQuit;
    }

    void OnEnable()
    {
        controls.GamePlayPF.RightFlipper.Enable();
	
    }

    void OnDisable()
    {
        controls.GamePlayPF.RightFlipper.Disable();
    }

    void Start()
    {
        currentAngle = restAngle;
controls.GamePlayPF.Quit.performed += OnQuit;
    }

    void Update()
    {
        bool pressed = controls.GamePlayPF.RightFlipper.IsPressed();

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
