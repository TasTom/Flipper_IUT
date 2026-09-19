using UnityEngine;
using UnityEngine.InputSystem; // <= Nouveau Input System

public class FlipperController : MonoBehaviour
{
    [Header("Angles du flipper")]
    public float restAngle = -20f;
    public float activeAngle = 45f;
    public float flipSpeed = 25f;

    [Header("Input Key")]
    public Key activationKey = Key.LeftArrow;   // Nouveau type "Key" du Input System

    private float currentAngle;

    void Start()
    {
        currentAngle = restAngle;
    }

    void Update()
    {
        bool pressed = Keyboard.current[activationKey].isPressed;

        float targetAngle = pressed ? activeAngle : restAngle;

        currentAngle = Mathf.Lerp(
            currentAngle,
            targetAngle,
            Time.deltaTime * flipSpeed
        );

        transform.localRotation = Quaternion.Euler(0, currentAngle, 0);
    }
}
