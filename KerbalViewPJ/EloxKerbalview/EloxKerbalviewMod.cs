using UnityEngine;
using SpaceWarp.API.Mods;
using KSP.Game;

namespace EloxKerbalview
{
    [MainMod]
    public class EloxKerbalviewMod : Mod
    {
        static bool loaded = false;
        private GameObject kerbalCameraObject = null;
        private Camera currentCamera;
        private KSP.Sim.impl.VesselComponent kerbal = null;
        Rect windowRect;
        bool drawUI = false;
        private int WINDOW_WIDTH = 500;
        private int WINDOW_HEIGHT = 1000;
        static float cameraForwardOffset = 10;
        static float cameraUpOffset = 12;
        static float cameraNearClipPlane = 8;
        static float cameraFOV = 90;
        string statusMsg = "Initialized";
        public override void Initialize() {
            Logger.Info("KerbalView is initialized");

            if (loaded) {
                Destroy(this);
            }
            loaded = true;

            base.Initialize();
        }

        void Awake() {
            windowRect = new Rect((Screen.width * 0.85f) - (WINDOW_WIDTH / 2), (Screen.height / 2) - (WINDOW_HEIGHT / 2), 0, 0);
        }

        
        void Update() {
            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.Alpha3)) drawUI = !drawUI;
            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.Alpha2) && findKerbal()) {
                if (!isFirstPersonViewEnabled()) {
                    statusMsg = "Enabling first person";

                    if (kerbalCameraObject) {
                        Destroy(kerbalCameraObject);
                    }

                    currentCamera = Camera.main;
                    var camera = Instantiate(Camera.main);
                    camera.fieldOfView = cameraFOV;
                    camera.nearClipPlane = 0.01f*cameraNearClipPlane;
                    kerbalCameraObject = camera.gameObject;
                    kerbalCameraObject.name = "KerbalCamera";
                    
                    currentCamera.enabled = false;
                    camera.enabled = true;
                } else {
                    returnCamera();
                }
            }

            if (isFirstPersonViewEnabled()) {
                if (kerbal != null) {
                    var kerbalBehavior = Game.ViewController.GetBehaviorIfLoaded(kerbal);
                    kerbalCameraObject.transform.rotation = kerbalBehavior.transform.rotation;
                    currentCamera.gameObject.transform.rotation = kerbalCameraObject.transform.rotation;
                    kerbalCameraObject.transform.position = kerbalBehavior.transform.position + 0.01f*cameraUpOffset*kerbalBehavior.transform.up + 0.01f*cameraForwardOffset*kerbalBehavior.transform.forward;       
                } else {
                    returnCamera();
                }
                
            }
        }

        void returnCamera() {
            statusMsg = "Returning camera";
            kerbalCameraObject.GetComponent<Camera>().enabled = false;
            if (currentCamera) {
                currentCamera.enabled = true;
            } else {
                Camera.main.enabled = true;
            }
        }

        bool isFirstPersonViewEnabled() {
            return kerbalCameraObject && kerbalCameraObject.GetComponent<Camera>().enabled && kerbalCameraObject.GetComponent<Camera>() == Camera.main;
        }

        bool findKerbal() {
            var activeVessel = GameManager.Instance.Game.ViewController.GetActiveSimVessel();
            kerbal = (activeVessel != null && activeVessel.IsKerbalEVA && GameManager.Instance.Game.GlobalGameState.GetGameState().IsFlightMode)? activeVessel : null;

            return kerbal != null;
        }
        void OnGUI() {
            if (drawUI) {
                windowRect = GUILayout.Window(
                    GUIUtility.GetControlID(FocusType.Passive),
                    windowRect,
                    FillWindow,
                    "Kerbal View Debugger",
                    GUILayout.Height(0),
                    GUILayout.Width(500));
            }
        }

        private void FillWindow(int windowID) {
            var boxStyle = GUI.skin.GetStyle("Box");
            GUILayout.BeginVertical();
            try {
                if (GameManager.Instance.Game) {
                    GUILayout.Label($"Active Vessel: {GameManager.Instance.Game.ViewController.GetActiveSimVessel().DisplayName}");
                    GUILayout.Label($"Is Kerbal: {GameManager.Instance.Game.ViewController.GetActiveSimVessel().IsKerbalEVA}");
                    GUILayout.Label($"Position: {Game.ViewController.GetBehaviorIfLoaded(kerbal).transform.position}");
                    GUILayout.Label($"Saved game camera: {currentCamera.tag}");
                    GUILayout.Label($"First person camera: {kerbalCameraObject.name}");
                } 
            } catch (Exception exception) {
                Logger.Info(exception.ToString());
            }

            GUILayout.Label($"Status: {statusMsg}");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Forward offset: ", GUILayout.Width(WINDOW_WIDTH / 2));
            var cameraForwardOffsetString = GUILayout.TextField(cameraForwardOffset.ToString());
            float.TryParse(cameraForwardOffsetString, out cameraForwardOffset);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Up offset: ", GUILayout.Width(WINDOW_WIDTH / 2));
            var cameraUpOffsetString = GUILayout.TextField(cameraUpOffset.ToString());
            float.TryParse(cameraUpOffsetString, out cameraUpOffset);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("FOV: ", GUILayout.Width(WINDOW_WIDTH / 2));
            var cameraFOVString = GUILayout.TextField(cameraFOV.ToString());
            float.TryParse(cameraFOVString, out cameraFOV);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Camera NearClip: ", GUILayout.Width(WINDOW_WIDTH / 2));
            var cameraNearClipPlaneString = GUILayout.TextField(cameraNearClipPlane.ToString());
            float.TryParse(cameraNearClipPlaneString, out cameraNearClipPlane);
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 500));

        }
    }

    
}

