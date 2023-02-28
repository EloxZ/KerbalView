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
        private Camera skyCamera;
        private Camera scaledCamera;
        private Vector3 savedPosition;
        private Quaternion savedRotation;
        private Quaternion savedSkyboxRotation, savedScaledCameraRotation;
        private Quaternion lastKerbalRotation;
        private float savedFov;
        double savedDistance;
        
        private KSP.Sim.impl.VesselComponent kerbal = null;
        private KSP.Sim.impl.VesselBehavior kerbalBehavior = null;
        
        Rect windowRect;
        bool drawUI = false;
        Transform savedParent;
        bool firstPersonEnabled = false;
        private int WINDOW_WIDTH = 500;
        private int WINDOW_HEIGHT = 1000;
        float savedNearClip;
        KSP.Sim.GimbalState savedGlimbal;
        double lastKerbalHeading, lastKerbalRoll, lastKerbalPitch;
        KSP.Sim.CameraMode savedCameraMode;
        Vector3 rotationDistance;
        double lastHeading = -1;
        static double headingDifferenceRatio = 10000;
        static double headingDifference;
        static double cameraHeadingOffset = 90;
        static float cameraNearClipPlane = 8;
        static float cameraFOV = 90;
        float timerRotation = 0.1f;
        static Vector3 currentCameraVelocity = Vector3.zero;
        float smoothTime = 0.001f;
        bool rotatedLastFrame = false;
        private Camera currentCamera;
        static float cameraForwardOffset = 10;
        static float cameraUpOffset = 12;
        double minDistance, maxDistance, minFov, maxFov, defaultFOV, defaultDistance;
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
            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.Alpha2) && findKerbal() && GameManager.Instance.Game.CameraManager.FlightCamera.Mode == KSP.Sim.CameraMode.Auto) {
                if (!isFirstPersonViewEnabled()) {
                    enableFirstPerson();
                } else {
                    disableFirstPerson();
                }
            }
            
        }

        void FixedUpdate() {
            if (isFirstPersonViewEnabled()) {
                if (!gameChangedCamera() && kerbal != null && kerbalBehavior != null) {
                    updateCameraPosition();
                } else {
                    disableFirstPerson();
                }
            }
        }

        bool gameChangedCamera() {
            return currentCamera && currentCamera != Camera.main || savedCameraMode != GameManager.Instance.Game.CameraManager.FlightCamera.Mode;
        }

        void updateCameraPosition() {
            statusMsg = "not moving sky";
            bool isRotating = Quaternion.Angle(lastKerbalRotation, kerbalBehavior.transform.rotation) > 0;
            timerRotation = (isRotating)? 0 : timerRotation + Time.deltaTime;

            if (isRotating || timerRotation < 0.05f) {
                statusMsg = "moving sky";
                GameManager.Instance.Game.CameraManager.FlightCamera.ActiveSolution.ModifyGimbalState(new KSP.Sim.GimbalStateIncremental()
                {
                    heading = new double?(kerbal.Heading + 90),
                    
                });
            }

            rotatedLastFrame = isRotating;
            lastKerbalRotation = kerbalBehavior.transform.rotation;
            

            // Move Sky
            //skyCamera.transform.rotation = kerbalBehavior.transform.rotation;
            //scaledCamera.transform.rotation = kerbalBehavior.transform.rotation;             
            // Keep custom FOV
            //GameManager.Instance.Game.CameraManager.FlightCamera.ActiveSolution.SetCameraFieldOfView(cameraFOV);
        }

        void enableFirstPerson() {
            statusMsg = "Enabling first person";

            currentCamera = Camera.main;

            savedCameraMode = GameManager.Instance.Game.CameraManager.FlightCamera.Mode;
            savedDistance = GameManager.Instance.Game.CameraManager.FlightCamera.ActiveSolution.GimbalState.distance;

            savedParent = currentCamera.transform.parent;
            savedFov = currentCamera.fieldOfView;
            savedNearClip = currentCamera.nearClipPlane;
            savedPosition = currentCamera.transform.position;
            savedRotation = currentCamera.transform.rotation;


            //GameManager.Instance.Game.CameraManager.DisableInput();
            //GameManager.Instance.Game.CameraManager.FlightCamera.Tweakables.minDistance = -0.01f*cameraForwardOffset;
            //GameManager.Instance.Game.CameraManager.FlightCamera.Tweakables.DefaultDistance = -0.01f*cameraForwardOffset;
            //GameManager.Instance.Game.CameraManager.FlightCamera.Tweakables.maxDistance = -0.01f*cameraForwardOffset;
            //GameManager.Instance.Game.CameraManager.FlightCamera.ActiveSolution.ModifyGimbalState(new KSP.Sim.GimbalStateIncremental()
            //{
            //   distance = new double?(-0.01f*cameraForwardOffset)
            //});

            //GameManager.Instance.Game.CameraManager.FlightCamera.Tweakables.minFOV = cameraFOV;
            //GameManager.Instance.Game.CameraManager.FlightCamera.Tweakables.defaultFOV = cameraFOV;
            //GameManager.Instance.Game.CameraManager.FlightCamera.ActiveSolution.SetCameraFieldOfView(cameraFOV);

            lastKerbalRotation = kerbalBehavior.transform.rotation;
            GameManager.Instance.Game.CameraManager.DisableInput();

            // Align sky with our little friend
            GameManager.Instance.Game.CameraManager.FlightCamera.ActiveSolution.ModifyGimbalState(new KSP.Sim.GimbalStateIncremental()
            {
                heading = new double?(kerbal.Heading + 90),
                distance = new double?(2)
            });

            //firstPersonEnabled = true;

            

            GameManager.Instance.Game.CameraManager.FlightCamera.ActiveSolution.SetCameraFieldOfView(cameraFOV);
            // Camera config
            currentCamera.fieldOfView = cameraFOV;
            currentCamera.nearClipPlane = 0.01f*cameraNearClipPlane;
            

            
            // Camera position
            currentCamera.transform.parent = kerbalBehavior.transform;
            currentCamera.transform.localRotation = Quaternion.identity;
            var targetPosition = kerbalBehavior.transform.position + 0.01f*cameraUpOffset*kerbalBehavior.transform.up + 0.01f*cameraForwardOffset*kerbalBehavior.transform.forward;
            currentCamera.transform.position = targetPosition;

            // Config Game Camera

            //GameManager.Instance.Game.CameraManager.SelectFlightCameraMode(KSP.Sim.CameraMode.None);


            
            
            
            // Get SkyBox camera
            //foreach (Camera c in Camera.allCameras) {
            //    if (c.gameObject.name == "FlightCameraSkybox_Main") skyCamera = c;
                //if (c.gameObject.name == "FlightCameraScaled_Main") scaledCamera = c;
            //}

            //savedSkyboxRotation = skyCamera.transform.rotation;

            // Swap cameras
            
            
            



            //savedScaledCameraRotation = scaledCamera.transform.rotation;

            //lastKerbalRotation = kerbalBehavior.transform.rotation;
            //lastKerbalHeading = kerbal.Heading + 90;
            //lastKerbalRoll = kerbal.Roll_HorizonRelative;
            //lastKerbalPitch = kerbal.Pitch_HorizonRelative;

            //https://answers.unity.com/questions/416169/finding-pitchrollyaw-from-quaternions.html
            //GameManager.Instance.Game.CameraManager.DisableInput();
            
            //lastKerbalRotation = kerbalBehavior.transform.rotation;

            
            
            //GameManager.Instance.Game.CameraManager.FlightCamera.ActiveSolution.ModifyGimbalState(new KSP.Sim.GimbalStateIncremental()
            //{
            //    heading = new double?(kerbal.Heading + 90),
            //    pitch = new double?(kerbal.Pitch_HorizonRelative),
            //    roll = new double?(kerbal.Roll_HorizonRelative)
            //});

            firstPersonEnabled = true;
        }

        void disableFirstPerson() {
            statusMsg = "Disabling First Person";

            // Reset variables
            kerbal = null;
            kerbalBehavior = null;

            currentCamera.transform.parent = savedParent;
            currentCamera.nearClipPlane = savedNearClip;

            GameManager.Instance.Game.CameraManager.targetGimbalState.distance = savedDistance;
            GameManager.Instance.Game.CameraManager.targetGimbalState.pan = Vector2.zero;
            //GameManager.Instance.Game.CameraManager.FocusFlightCameraOnOAB();
            GameManager.Instance.Game.CameraManager.EnableInput();
            

             

    

            //GameManager.Instance.Game.CameraManager.FlightCamera.Tweakables.minDistance = 2;
            //GameManager.Instance.Game.CameraManager.FlightCamera.Tweakables.DefaultDistance = 8;
            //GameManager.Instance.Game.CameraManager.FlightCamera.Tweakables.maxDistance =  80000.0;
            //GameManager.Instance.Game.CameraManager.FlightCamera.Tweakables.defaultFOV = 60;

            
            //GameManager.Instance.Game.CameraManager.FlightCamera.Tweakables.DefaultDistance = -0.01f*cameraForwardOffset;
            //GameManager.Instance.Game.CameraManager.FlightCamera.Tweakables.maxDistance = -0.01f*cameraForwardOffset;
            //GameManager.Instance.Game.CameraManager.FlightCamera.ActiveSolution.ModifyGimbalState(new KSP.Sim.GimbalStateIncremental()
            //{
            //    distance = new double?(8)
            //});

            //GameManager.Instance.Game.CameraManager.FlightCamera.Tweakables.minFOV = 60;
            //GameManager.Instance.Game.CameraManager.FlightCamera.Tweakables.defaultFOV = 60;
            //GameManager.Instance.Game.CameraManager.FlightCamera.ActiveSolution.SetCameraFieldOfView(60);

            firstPersonEnabled = false;

            
            
            
            

            

            //GameManager.Instance.Game.CameraManager.FlightCamera.ActiveSolution.ResetGimbalAndCamera();
            


            //GameManager.Instance.Game.CameraManager.SelectFlightCameraMode(KSP.Sim.CameraMode.None);
            //
        }

        bool isFirstPersonViewEnabled() {
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
                    //GUILayout.Label($"Position: {kerbalBehavior.transform.position}");
                    //GUILayout.Label($"Rotation: {kerbalBehavior.transform.rotation}");
                    //GUILayout.Label($"Euler Angles: {kerbalBehavior.transform.rotation.eulerAngles}");
                    GUILayout.Label($"Kerbal localrotation: {kerbal.mainBody.transform.localRotation.eulerAngles}");
                    GUILayout.Label($"Kerbal localrotation: {kerbal.mainBody.Rotation.localRotation}");
                    //GUILayout.Label($"Camera pitch: {GameManager.Instance.Game.CameraManager.FlightCamera.ActiveSolution.GimbalState.pitch}");
                    //GUILayout.Label($"Camera roll: {GameManager.Instance.Game.CameraManager.FlightCamera.ActiveSolution.GimbalState.roll}");
                    //GUILayout.Label($"Camera pan: {GameManager.Instance.Game.CameraManager.FlightCamera.ActiveSolution.GimbalState.pan}");
                    //GUILayout.Label($"Camera localHeading: {GameManager.Instance.Game.CameraManager.FlightCamera.ActiveSolution.GimbalState.localHeading}");
                    //GUILayout.Label($"Camera localPitch: {GameManager.Instance.Game.CameraManager.FlightCamera.ActiveSolution.GimbalState.localPitch}");
                    //GUILayout.Label($"Unity camera position: {Camera.main.transform.position}");
                    //GUILayout.Label($"Unity camera rotation: {Camera.main.transform.rotation}");
                    //GUILayout.Label($"Unity camera rotation Euler: {Camera.main.transform.rotation.eulerAngles}");
                    //GUILayout.Label($"Unity camera rotation Euler: {Camera.main.transform.parent.rotation.eulerAngles}");
                    //GUILayout.Label($"Unity camera Fov: {Camera.main.fieldOfView}");
                    //GUILayout.Label($"Unity camera NearClip: {Camera.main.nearClipPlane}");
                    //GUILayout.Label($"Saved Parent: {savedParent}");

                    
                     
                    //GUILayout.Label($"Current Star Direction: {GameManager.Instance.Game.GraphicsManager.GetCurrentStarDirection().vector}");
                    //GUILayout.Label($"Kerbal Heading: {kerbal.Heading}");
                    //GUILayout.Label($"Kerbal Roll HorizonRelative: {kerbal.Roll_HorizonRelative}");
                    //GUILayout.Label($"Kerbal Pitch HorizonRelative: {kerbal.Pitch_HorizonRelative}");
                    //GUILayout.Label($"Kerbal MainBody rotation angle: {kerbal.mainBody.rotationAngle}");
                    //GUILayout.Label($"Kerbal Direct Rot Angle: {kerbal.mainBody.directRotAngle}");

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
            GUILayout.Label($"First Person Enabled: {isFirstPersonViewEnabled()}");
            GUILayout.Label($"Main camera: {Camera.main.name}");
            if (currentCamera) GUILayout.Label($"Current camera: {currentCamera.name}{" "}{currentCamera.enabled}");
            if (kerbalCameraObject) GUILayout.Label($"My camera: {kerbalCameraObject.GetComponent<Camera>().name}{" "}{kerbalCameraObject.GetComponent<Camera>().enabled}");
            
            //GUILayout.BeginHorizontal();
            //GUILayout.Label("Heading Difference Ratio: ", GUILayout.Width(WINDOW_WIDTH / 2));
            //var headingDifferenceRatioString = GUILayout.TextField(headingDifferenceRatio.ToString());
            //double.TryParse(headingDifferenceRatioString, out headingDifferenceRatio);
            //GUILayout.EndHorizontal();

            //GUILayout.BeginHorizontal();
            //GUILayout.Label("Forward offset: ", GUILayout.Width(WINDOW_WIDTH / 2));
            //var cameraForwardOffsetString = GUILayout.TextField(cameraForwardOffset.ToString());
            //float.TryParse(cameraForwardOffsetString, out cameraForwardOffset);
            //GUILayout.EndHorizontal();
            
            //GUILayout.BeginHorizontal();
            //GUILayout.Label("Forward offset: ", GUILayout.Width(WINDOW_WIDTH / 2));
            //var cameraForwardOffsetString = GUILayout.TextField(cameraForwardOffset.ToString());
            //float.TryParse(cameraForwardOffsetString, out cameraForwardOffset);
            //GUILayout.EndHorizontal();

            //GUILayout.BeginHorizontal();
            //GUILayout.Label("Up offset: ", GUILayout.Width(WINDOW_WIDTH / 2));
            //var cameraUpOffsetString = GUILayout.TextField(cameraUpOffset.ToString());
            //float.TryParse(cameraUpOffsetString, out cameraUpOffset);
            //GUILayout.EndHorizontal();

            //GUILayout.BeginHorizontal();
            //GUILayout.Label("Camera heading offset: ", GUILayout.Width(WINDOW_WIDTH / 2));
            //var cameraHeadingOffsetString = GUILayout.TextField(cameraHeadingOffset.ToString());
            //double.TryParse(cameraHeadingOffsetString, out cameraHeadingOffset);
            //GUILayout.EndHorizontal();

            //GUILayout.BeginHorizontal();/
            //GUILayout.Label("FOV: ", GUILayout.Width(WINDOW_WIDTH / 2));
            //var cameraFOVString = GUILayout.TextField(cameraFOV.ToString());
            //float.TryParse(cameraFOVString, out cameraFOV);
            //GUILayout.EndHorizontal();

           //GUILayout.BeginHorizontal();
            //GUILayout.Label("Camera NearClip: ", GUILayout.Width(WINDOW_WIDTH / 2));
            //var cameraNearClipPlaneString = GUILayout.TextField(cameraNearClipPlane.ToString());
            //float.TryParse(cameraNearClipPlaneString, out cameraNearClipPlane);
            //GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 500));

        }
    }

    
}

