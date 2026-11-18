using UnityEngine;
using RecallRoom.Core;
using RecallRoom.UI;
using RecallRoom.Audio;

namespace RecallRoom.XR
{
    /// <summary>
    /// Fallback desktop simulator providing mouse & keyboard input controls
    /// for Unity Editor and automated test suites when a physical Quest headset is not attached.
    /// </summary>
    public class XRSimulatorFallback : MonoBehaviour
    {
        [Header("Desktop Control Settings")]
        [SerializeField] private float mouseSensitivity = 2.0f;
        [SerializeField] private float movementSpeed = 1.5f;
        [SerializeField] private bool enableSimulator = true;

        [Header("References")]
        [SerializeField] private HandTrackingManager handTrackingManager;
        [SerializeField] private Transform headCameraTransform;

        private float rotationX = 0f;
        private float rotationY = 0f;
        private bool isDraggingMouse = false;

        private void Start()
        {
            if (headCameraTransform == null && Camera.main != null)
            {
                headCameraTransform = Camera.main.transform;
            }

            if (handTrackingManager == null)
            {
                handTrackingManager = GetComponent<HandTrackingManager>() ?? FindObjectOfType<HandTrackingManager>();
            }

            // Only activate if not running on real headset
            #if UNITY_ANDROID && !UNITY_EDITOR
            enableSimulator = false;
            #endif
        }

        private void Update()
        {
            if (!enableSimulator) return;

            HandleMouseLook();
            HandleKeyboardShortcuts();
            HandleSimulatedHandInput();
        }

        private void HandleMouseLook()
        {
            if (headCameraTransform == null) return;

            // Right-click drag rotates head gaze
            if (Input.GetMouseButtonDown(1))
            {
                isDraggingMouse = true;
            }
            if (Input.GetMouseButtonUp(1))
            {
                isDraggingMouse = false;
            }

            if (isDraggingMouse)
            {
                float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
                float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

                rotationY += mouseX;
                rotationX -= mouseY;
                rotationX = Mathf.Clamp(rotationX, -80f, 80f);

                headCameraTransform.localRotation = Quaternion.Euler(rotationX, rotationY, 0f);
            }

            // WASD subtle seated repositioning
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");
            if (Mathf.Abs(horizontal) > 0.01f || Mathf.Abs(vertical) > 0.01f)
            {
                Vector3 moveDir = (headCameraTransform.right * horizontal + headCameraTransform.forward * vertical);
                moveDir.y = 0f;
                headCameraTransform.position += moveDir.normalized * (movementSpeed * Time.deltaTime);
            }
        }

        private void HandleSimulatedHandInput()
        {
            if (handTrackingManager == null || headCameraTransform == null) return;

            // Position simulated right hand pointer in front of camera
            Transform rHand = handTrackingManager.GetHandTransform(HandType.Right);
            if (rHand != null)
            {
                rHand.position = headCameraTransform.position + (headCameraTransform.forward * 0.45f) + (headCameraTransform.right * 0.18f) - (headCameraTransform.up * 0.12f);
                rHand.rotation = headCameraTransform.rotation;
            }

            // Left click triggers pinch/grab ray
            if (Input.GetMouseButtonDown(0))
            {
                handTrackingManager.TriggerSimulatedPinchStart(HandType.Right, rHand != null ? rHand.position : headCameraTransform.position);
            }
            if (Input.GetMouseButtonUp(0))
            {
                handTrackingManager.TriggerSimulatedPinchEnd(HandType.Right);
            }
        }

        private void HandleKeyboardShortcuts()
        {
            // Spacebar: Toggle Voice Recording or submit recall answer
            if (Input.GetKeyDown(KeyCode.Space))
            {
                Debug.Log("[XRSimulatorFallback] Space pressed: Toggling voice recording / recall evaluation.");
                if (WorldSpaceUIManager.Instance != null && RecallStateManager.Instance != null)
                {
                    if (RecallStateManager.Instance.CurrentState == RecallState.RECALLING)
                    {
                        WorldSpaceUIManager.Instance.SubmitActiveRecallVoice();
                    }
                    else if (RecallStateManager.Instance.CurrentState == RecallState.READY_TO_RECALL)
                    {
                        RecallStateManager.Instance.BeginRecall();
                    }
                }
            }

            // 'H': Toggle Hint on active card
            if (Input.GetKeyDown(KeyCode.H))
            {
                Debug.Log("[XRSimulatorFallback] 'H' pressed: Toggling active card hint.");
                var cardView = FindObjectOfType<SpatialCardView>();
                if (cardView != null)
                {
                    cardView.ToggleHint();
                }
            }

            // 'R': Recenter workspace
            if (Input.GetKeyDown(KeyCode.R))
            {
                Debug.Log("[XRSimulatorFallback] 'R' pressed: Recentering tracking workspace.");
                if (XRBootstrap.Instance != null)
                {
                    XRBootstrap.Instance.RecenterTrackingOrigin();
                }
            }

            // Escape: Go Home
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (RecallStateManager.Instance != null)
                {
                    RecallStateManager.Instance.GoHome();
                }
            }
        }
    }
}
