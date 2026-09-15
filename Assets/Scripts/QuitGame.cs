using UnityEngine;
using UnityEngine.InputSystem;

public class QuitGame : MonoBehaviour
{
    private PinballControls controls;
    private InputAction quitAction;

    void Awake()
    {
        controls = new PinballControls();
        quitAction = controls.GamePlayPF.Quit;
    }

    void OnEnable()
    {
        quitAction.Enable();
    }

    void OnDisable()
    {
        quitAction.Disable();
    }

    void Update()
    {
        if (quitAction.WasPressedThisFrame())
        {
            Quit();
        }
    }

    void Quit()
    {
        Debug.Log("Quit requested");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
