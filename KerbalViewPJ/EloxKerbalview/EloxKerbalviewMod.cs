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
        private Camera flightCamera;
        private Vector3 savedPosition;
        private Quaternion savedRotation;
        
        private KSP.Sim.impl.VesselComponent kerbal = null;
        private KSP.Sim.impl.VesselBehavior kerbalBehavior = null;
        Rect windowRect;
        bool drawUI = false;
        bool firstPersonEnabled = false;
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
                    enableFirstPerson();
                } else {
                    disableFirstPerson();
                }
            }

            if (isFirstPersonViewEnabled()) {
                if (kerbal != null && kerbalBehavior != null) {
                    updateCameraPosition();
                } else {
                    disableFirstPerson();
                }
            }
        }

        void updateCameraPosition() {
                flightCamera.transform.rotation = kerbalBehavior.transform.rotation;
                flightCamera.transform.position = kerbalBehavior.transform.position + 0.01f*cameraUpOffset*kerbalBehavior.transform.up + 0.01f*cameraForwardOffset*kerbalBehavior.transform.forward;       
        }

        void enableFirstPerson() {
            statusMsg = "Enabling first person";
            GameManager.Instance.Game.CameraManager.SelectFlightCameraMode(KSP.Sim.CameraMode.None);
            flightCamera = Camera.main;
            savedPosition = flightCamera.transform.position;
            savedRotation = flightCamera.transform.rotation;
            flightCamera.fieldOfView = cameraFOV;
            flightCamera.nearClipPlane = 0.01f*cameraNearClipPlane;
            
            firstPersonEnabled = true;
        }

        void disableFirstPerson() {
            statusMsg = "Disabling First Person";
            kerbal = null;
            if (flightCamera) {
                flightCamera.transform.position = savedPosition;
                flightCamera.transform.rotation = savedRotation;
            }
            GameManager.Instance.Game.CameraManager.SelectFlightCameraMode(KSP.Sim.CameraMode.Auto);
            firstPersonEnabled = false;
        }

        bool isFirstPersonViewEnabled() {
            firstPersonEnabled = firstPersonEnabled && GameManager.Instance.Game.CameraManager.FlightCamera.Mode == KSP.Sim.CameraMode.None;
            return firstPersonEnabled;
        }

        bool findKerbal() {
            var activeVessel = GameManager.Instance.Game.ViewController.GetActiveSimVessel();
            kerbal = (activeVessel != null && activeVessel.IsKerbalEVA && GameManager.Instance.Game.GlobalGameState.GetGameState().IsFlightMode)? activeVessel : null;
            kerbalBehavior = Game.ViewController.GetBehaviorIfLoaded(kerbal);
            return kerbal != null && kerbalBehavior != null;
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

        void FillWindow(int windowID) {
            var boxStyle = GUI.skin.GetStyle("Box");
            GUILayout.BeginVertical();
            try {
                if (kerbal != null && GameManager.Instance.Game) {
                    GUILayout.Label($"Active Vessel: {GameManager.Instance.Game.ViewController.GetActiveSimVessel().DisplayName}");
                    GUILayout.Label($"Is Kerbal: {GameManager.Instance.Game.ViewController.GetActiveSimVessel().IsKerbalEVA}");
                    GUILayout.Label($"Position: {kerbalBehavior.transform.position}");
                    GUILayout.Label($"Current Camera Name: {GameManager.Instance.Game.CameraManager.FlightCamera.Tweakables.gameObject.name}");
                    GUILayout.Label($"Current Camera Position: {GameManager.Instance.Game.CameraManager.FlightCamera.Tweakables.gameObject.transform.position}");
                    //GUILayout.Label($"Current Camera Position: {GameManager.Instance.Game.CameraManager.FlightCamera.ActiveSolution.CameraShot.CameraPosition.ToString()}");
                    //GUILayout.Label($"Current Camera Position: {GameManager.Instance.Game.CameraManager.PrimaryScreenCameraShot.CameraPosition.ToString()}");

                } 
            } catch (Exception exception) {
                Logger.Info(exception.ToString());
            }

            GUILayout.Label($"Status: {statusMsg}");
            GUILayout.Label($"First Person Enabled: {firstPersonEnabled}");

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

