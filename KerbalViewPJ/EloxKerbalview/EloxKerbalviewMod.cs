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
        private float savedFov;
        
        private KSP.Sim.impl.VesselComponent kerbal = null;
        private KSP.Sim.impl.VesselBehavior kerbalBehavior = null;
        Rect windowRect;
        bool drawUI = false;
        bool firstPersonEnabled = false;
        private int WINDOW_WIDTH = 500;
        private int WINDOW_HEIGHT = 1000;
        double lastHeading = -1;
        static double headingDifferenceRatio = 10000;
        static double headingDifference;
        static double cameraHeadingOffset = 90;
        static float cameraNearClipPlane = 8;
        static float cameraFOV = 90;
        
        static float cameraForwardOffset = 10;
        static float cameraUpOffset = 12;
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
            //if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.Alpha4)) 
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
            //var glimbalState = new KSP.Sim.GimbalState();
            //glimbalState.heading = kerbal.Heading;
            //glimbalState.roll = kerbal.Roll_HorizonRelative;
            //glimbalState.pitch = kerbal.Pitch_HorizonRelative;
            //glimbalState.pan = new Vector2(200, 200);
            //glimbalState.distance = 200;
            var kerbalHeading = kerbal.Heading;
            headingDifference = Math.Abs(kerbalHeading-lastHeading);
            var newHeading = (headingDifference >= 0.0001*headingDifferenceRatio)? kerbalHeading : lastHeading;
            lastHeading = kerbalHeading;
            
            
            //GameManager.Instance.Game.CameraManager.targetGimbalState = glimbalState;
            //GameManager.Instance.Game.CameraManager.FlightCamera.ActiveSolution.SetGimbalState(glimbalState);
            //GameManager.Instance.Game.CameraManager.CameraPanValue = new Vector2(kerbalBehavior.transform.rotation.w,kerbalBehavior.transform.rotation.y);
            GameManager.Instance.Game.CameraManager.FlightCamera.ActiveSolution.ModifyGimbalState(new KSP.Sim.GimbalStateIncremental()
            {
                heading = new double?(newHeading + cameraHeadingOffset)
            });
            GameManager.Instance.Game.CameraManager.FlightCamera.ActiveSolution.SetCameraFieldOfView(cameraFOV);
            flightCamera.transform.position = kerbalBehavior.transform.position + 0.01f*cameraUpOffset*kerbalBehavior.transform.up + 0.01f*cameraForwardOffset*kerbalBehavior.transform.forward;
            //flightCamera.transform.rotation = kerbalBehavior.transform.rotation;
        }

        void enableFirstPerson() {
            statusMsg = "Enabling first person";
            
            //GameManager.Instance.Game.CameraManager.SelectFlightCameraMode(KSP.Sim.CameraMode.None);
            flightCamera = Camera.main;

            
            flightCamera.nearClipPlane = 0.01f*cameraNearClipPlane;
            
            firstPersonEnabled = true;
        }

        void disableFirstPerson() {
            
            statusMsg = "Disabling First Person";
            firstPersonEnabled = false;
            kerbal = null;

            GameManager.Instance.Game.CameraManager.FlightCamera.ActiveSolution.ResetGimbalAndCamera();
            GameManager.Instance.Game.CameraManager.FlightCamera.ActiveSolution.ModifyGimbalState(new KSP.Sim.GimbalStateIncremental()
            {
                distance = new double?(10)
            });
            
        }

        bool isFirstPersonViewEnabled() {
            firstPersonEnabled = firstPersonEnabled && GameManager.Instance.Game.CameraManager.FlightCamera.Mode == KSP.Sim.CameraMode.Auto;
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
                if (GameManager.Instance.Game) {
                    
                    //GUILayout.Label($"Active Vessel: {GameManager.Instance.Game.ViewController.GetActiveSimVessel().DisplayName}");
                    //GUILayout.Label($"Is Kerbal: {GameManager.Instance.Game.ViewController.GetActiveSimVessel().IsKerbalEVA}");
                    GUILayout.Label($"Position: {kerbalBehavior.transform.position}");
                    GUILayout.Label($"Rotation: {kerbalBehavior.transform.rotation}");
                    GUILayout.Label($"Euler Angles: {kerbalBehavior.transform.rotation.eulerAngles}");
                    GUILayout.Label($"Camera heading: {GameManager.Instance.Game.CameraManager.FlightCamera.ActiveSolution.GimbalState.heading}");
                    GUILayout.Label($"Camera pitch: {GameManager.Instance.Game.CameraManager.FlightCamera.ActiveSolution.GimbalState.pitch}");
                    GUILayout.Label($"Camera roll: {GameManager.Instance.Game.CameraManager.FlightCamera.ActiveSolution.GimbalState.roll}");
                    GUILayout.Label($"Camera pan: {GameManager.Instance.Game.CameraManager.FlightCamera.ActiveSolution.GimbalState.pan}");
                    GUILayout.Label($"Unity camera position: {Camera.main.transform.position}");
                    GUILayout.Label($"Unity camera rotation: {Camera.main.transform.rotation}");
                    GUILayout.Label($"Unity camera rotation Euler: {Camera.main.transform.rotation.eulerAngles}");
                    GUILayout.Label($"Unity camera Fov: {Camera.main.fieldOfView}");
                    GUILayout.Label($"Unity camera NearClip: {Camera.main.nearClipPlane}");
                    GUILayout.Label($"Current Heading: {kerbal.Heading}");

                    
                     
                    //GUILayout.Label($"Current Star Direction: {GameManager.Instance.Game.GraphicsManager.GetCurrentStarDirection().vector}");
                    //GUILayout.Label($"Current Camera: {GameManager.Instance.Game.GraphicsManager.GetCurrentUnityCamera()}");
                    //GUILayout.Label($"Observed body: {GameManager.Instance.Game.GraphicsManager.GetObservedBody()}");
                    //GUILayout.Label($"Observed body: {flightCamera.gameObject.}");
                    
                    
                    //GUILayout.Label($"Current Camera Position: {GameManager.Instance.Game.CameraManager.FlightCamera.ActiveSolution.CameraShot.CameraPosition.ToString()}");
                    //GUILayout.Label($"Current Camera Pan: {GameManager.Instance.Game.CameraManager.CameraPanValue.x}{" "}{GameManager.Instance.Game.CameraManager.CameraPanValue.y}");
                    //GUILayout.Label($"Current Camera Glimbal: {"H"}{GameManager.Instance.Game.CameraManager.targetGimbalState.heading}{"P"}{GameManager.Instance.Game.CameraManager.targetGimbalState.pitch}{"R"}{GameManager.Instance.Game.CameraManager.targetGimbalState.roll}");
                    //GUILayout.Label($"Camera Anchor: {GameManager.Instance.Game.CameraManager.FlightCamera.Anchor.ToString()}");
                    
                } 
            } catch (Exception exception) {
                Logger.Info(exception.ToString());
            }

            GUILayout.Label($"Status: {statusMsg}");
            GUILayout.Label($"First Person Enabled: {firstPersonEnabled}");
            GUILayout.Label($"Heading difference: {headingDifference}");
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("Heading Difference Ratio: ", GUILayout.Width(WINDOW_WIDTH / 2));
            var headingDifferenceRatioString = GUILayout.TextField(headingDifferenceRatio.ToString());
            double.TryParse(headingDifferenceRatioString, out headingDifferenceRatio);
            GUILayout.EndHorizontal();


            
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
            GUILayout.Label("Camera heading offset: ", GUILayout.Width(WINDOW_WIDTH / 2));
            var cameraHeadingOffsetString = GUILayout.TextField(cameraHeadingOffset.ToString());
            double.TryParse(cameraHeadingOffsetString, out cameraHeadingOffset);
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

