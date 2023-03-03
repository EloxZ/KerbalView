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
        Transform savedParent, savedSkyParent, savedScaledParent, savedSky, savedScaled, savedCamera;
        bool firstPersonEnabled = false;
        private int WINDOW_WIDTH = 500;
        private int WINDOW_HEIGHT = 1000;
        float savedNearClip;
        KSP.Sim.GimbalState savedGlimbal;
        double lastKerbalHeading, lastKerbalRoll, lastKerbalPitch;
        KSP.Sim.CameraMode savedCameraMode;
        float currentVelocity;
        Vector3 rotationDistance;
        double lastHeading = -1;
        static double headingDifferenceRatio = 10000;
        static double headingDifference;
        static double cameraHeadingOffset = 90;
        static float cameraNearClipPlane = 8;
        static float cameraFOV = 90;
        float timerRotation = 0.1f;
        static Vector3 currentCameraVelocity = Vector3.zero;
        static float smoothTime = 0.1f;
        float lastKerbalYRotation;
        bool rotatedLastFrame = false;
        double lastTime;
        private Camera currentCamera;
        static float cameraForwardOffset = 10;
        static float cameraUpOffset = 12;
        static GameObject helmetLights;
        float range = 20, spotAngle = 45, lightIntesity = 100;

        string statusMsg = "Initialized";
        public override void OnInitialized() {
            Logger.Info("KerbalView is initialized");

            if (loaded) {
                Destroy(this);
            }

            loaded = true;
        }

        void Start() {
            firstPersonEnabled = false;
        }

        void Awake() {
            windowRect = new Rect((Screen.width * 0.85f) - (WINDOW_WIDTH / 2), (Screen.height / 2) - (WINDOW_HEIGHT / 2), 0, 0);
        }
  
        void Update() {
            if (kerbal == null) findKerbal();
            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.Alpha4)) turnOnHelmetLights();
            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.Alpha3)) drawUI = !drawUI;
            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.Alpha2) && GameManager.Instance.Game.CameraManager.FlightCamera.Mode == KSP.Sim.CameraMode.Auto) {
                if (!isFirstPersonViewEnabled()) {
                    enableFirstPerson();
                } else {
                    disableFirstPerson();
                }
            }

            if (isFirstPersonViewEnabled() && gameChangedCamera()) disableFirstPerson();
            if (isFirstPersonViewEnabled()) updateStars();
            
        }

        void turnOnHelmetLights() {
            if (helmetLights) {
                Destroy(helmetLights);
            } else if (kerbalBehavior) {
                helmetLights = new GameObject("EVA_HelmetLight");
                GameObject helmetLightLeft = new GameObject("EVA_HelmetLightLeft");
                GameObject helmetLightRight = new GameObject("EVA_HelmetLightRight");

                helmetLights.transform.parent = kerbalBehavior.transform;
                helmetLightLeft.transform.parent = helmetLights.transform;
                helmetLightRight.transform.parent = helmetLights.transform;

                helmetLights.transform.localPosition = new Vector3(0, 0.12f, 0.1f);
                helmetLightLeft.transform.localPosition = new Vector3(0.3f, 0, 0);
                helmetLightRight.transform.localPosition = new Vector3(-0.3f, 0, 0);

                helmetLights.transform.localRotation = Quaternion.identity;
                helmetLightLeft.transform.localEulerAngles = new Vector3(8, 5, 0);
                helmetLightRight.transform.localEulerAngles = new Vector3(8, -5, 0);

                Light insideLight = helmetLights.AddComponent<Light>();
                Light lightCompLeft = helmetLightLeft.AddComponent<Light>();
                Light lightCompRight = helmetLightRight.AddComponent<Light>();

                lightCompLeft.type = LightType.Spot;
                lightCompRight.type = LightType.Spot;

                lightCompLeft.color = Color.white;
                lightCompLeft.range = range;
                lightCompLeft.spotAngle = spotAngle;
                lightCompLeft.intensity = 0.01f * lightIntesity;

                lightCompRight.color = Color.white;
                lightCompRight.range = range;
                lightCompRight.spotAngle = spotAngle;
                lightCompRight.intensity = 0.01f * lightIntesity;

                insideLight.color = Color.white;
                insideLight.intensity = 2;
                insideLight.range = 0.5f;
            }
        }

        void updateStars() {
            var movement = currentCamera.transform.rotation.eulerAngles.y - lastKerbalYRotation;
            lastKerbalYRotation = currentCamera.transform.rotation.eulerAngles.y;

            var targetY = skyCamera.transform.eulerAngles.y + movement;
            
            skyCamera.transform.eulerAngles = new Vector3(currentCamera.transform.eulerAngles.x, targetY, currentCamera.transform.eulerAngles.z);
            scaledCamera.transform.eulerAngles = new Vector3(currentCamera.transform.eulerAngles.x, targetY, currentCamera.transform.eulerAngles.z);
        }

        bool gameChangedCamera() {
            return currentCamera != Camera.main || GameManager.Instance.Game.CameraManager.FlightCamera.Mode != KSP.Sim.CameraMode.Auto || GameManager.Instance.Game.ViewController.GetActiveSimVessel() != kerbal;
        }

        void enableFirstPerson() {
            // Take control of the camera
            GameManager.Instance.Game.CameraManager.DisableInput();

            try {
                currentCamera = Camera.main;

                // Get SkyBox and Scaled camera
                foreach (Camera c in Camera.allCameras) {
                    if (c.gameObject.name == "FlightCameraSkybox_Main") {
                        skyCamera = c;
                    } else if (c.gameObject.name == "FlightCameraScaled_Main") { 
                        scaledCamera = c;
                    }
                }

                // Save config
                savedParent = currentCamera.transform.parent;
                savedRotation = currentCamera.transform.localRotation;
                savedPosition = currentCamera.transform.localPosition;

                savedFov = currentCamera.fieldOfView;
                savedNearClip = currentCamera.nearClipPlane;

                // Camera config
                currentCamera.fieldOfView = cameraFOV;
                currentCamera.nearClipPlane = 0.01f*cameraNearClipPlane;

                // Current sky deviation caused by time
                var time = skyCamera.transform.eulerAngles.y - currentCamera.transform.eulerAngles.y;

                // Anchor camera to our little friend
                currentCamera.transform.parent = kerbalBehavior.transform;
                currentCamera.transform.localRotation = Quaternion.identity;
                var targetPosition = kerbalBehavior.transform.position + 0.01f*cameraUpOffset*kerbalBehavior.transform.up + 0.01f*cameraForwardOffset*kerbalBehavior.transform.forward;
                currentCamera.transform.position = targetPosition;
                
                // Sync cameras and desync by time
                skyCamera.transform.rotation = currentCamera.transform.rotation;
                scaledCamera.transform.rotation = currentCamera.transform.rotation;
                skyCamera.transform.eulerAngles += new Vector3(0,time,0);
                scaledCamera.transform.eulerAngles += new Vector3(0,time,0);

                lastKerbalYRotation = currentCamera.transform.rotation.eulerAngles.y;

                firstPersonEnabled = true;
            } catch (Exception exception) {
                // For unknown error cases
                Logger.Info(exception.Message);
                GameManager.Instance.Game.CameraManager.EnableInput();
            }

            
        }

        void disableFirstPerson() {
            // To avoid NullRefs
            if (currentCamera && skyCamera && scaledCamera) {
                var time = skyCamera.transform.eulerAngles.y - currentCamera.transform.eulerAngles.y;

                // Revert changes
                currentCamera.transform.parent = savedParent;
                currentCamera.transform.localPosition = savedPosition;
                currentCamera.transform.localRotation = savedRotation;

                // Sync cameras and desync by time
                skyCamera.transform.rotation = currentCamera.transform.rotation;
                scaledCamera.transform.rotation = currentCamera.transform.rotation;
                skyCamera.transform.eulerAngles += new Vector3(0,time,0);
                scaledCamera.transform.eulerAngles += new Vector3(0,time,0);

                // Reset local rotations (tends to variate a bit with movement)
                skyCamera.transform.localRotation = Quaternion.identity;
                scaledCamera.transform.localRotation = Quaternion.identity;

                currentCamera.nearClipPlane = savedNearClip;
                currentCamera.fieldOfView = savedFov;
            }
            
            GameManager.Instance.Game.CameraManager.EnableInput();

            firstPersonEnabled = false;
        }

        bool isFirstPersonViewEnabled() {
            return firstPersonEnabled;
        }

        bool findKerbal() {
            var activeVessel = GameManager.Instance.Game.ViewController.GetActiveSimVessel();
            kerbal = (activeVessel != null && activeVessel.IsKerbalEVA && GameManager.Instance.Game.GlobalGameState.GetGameState().IsFlightMode)? activeVessel : null;
            if (kerbal != null) {
                kerbalBehavior = Game.ViewController.GetBehaviorIfLoaded(kerbal);
                KSP.Sim.Definitions.ModuleAction toggleLightsAction = new KSP.Sim.Definitions.ModuleAction((Delegate)turnOnHelmetLights);
                kerbal.SimulationObject.Kerbal.KerbalData.AddAction("Toggle Helmet Lights", toggleLightsAction);
            }
            
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
                    //Camera skyCameraDeb = null, scaledCameraDeb = null;

                    //LODBehavior[] myItems = FindObjectsOfType(typeof(LODBehavior)) as LODBehavior[];
                    //GUILayout.Label($"Found " + myItems.Length + " instances with this script attached");
                    //foreach(LODBehavior item in myItems)
                    //{
                    //    GUILayout.Label($"Script gameobject name: {item.gameObject.name}");
                    //    GUILayout.Label($"Update bounds : {item.alwaysUpdateBounds}");
                    //}
                    
                    //GUILayout.Label($"Active Vessel: {GameManager.Instance.Game.ViewController.GetActiveSimVessel().DisplayName}");
                    //GUILayout.Label($"Is Kerbal: {GameManager.Instance.Game.ViewController.GetActiveSimVessel().IsKerbalEVA}");
                    //GUILayout.Label($"Position: {kerbalBehavior.transform.position}");
                    //GUILayout.Label($"Rotation: {kerbalBehavior.transform.rotation}");
                    //GUILayout.Label($"Euler Angles: {kerbalBehavior.transform.rotation.eulerAngles}");
                    //GUILayout.Label($"Main Pos: {Camera.main.transform.position}");
                    //GUILayout.Label($"Skybox Pos: {skyCameraDeb.transform.position}");
                    //GUILayout.Label($"Scaled Pos: {scaledCameraDeb.transform.position}");

                    //GUILayout.Label($"Main Rot: {Camera.main.transform.rotation.eulerAngles}");
                    //GUILayout.Label($"Skybox Rot: {skyCameraDeb.transform.rotation.eulerAngles}");
                    //GUILayout.Label($"Scaled Rot: {scaledCameraDeb.transform.rotation.eulerAngles}");

                    //GUILayout.Label($"Main Parent: {Camera.main.transform.parent.name}");
                    //GUILayout.Label($"Skybox Parent: {skyCameraDeb.transform.parent.name}");
                   // GUILayout.Label($"Scaled Parent: {scaledCameraDeb.transform.parent.name}");

                    //GUILayout.Label($"Main LocalRot: {Camera.main.transform.localRotation.eulerAngles}");
                    //GUILayout.Label($"Skybox LocalRot: {skyCameraDeb.transform.localRotation.eulerAngles}");
                    //GUILayout.Label($"Scaled LocalRot: {scaledCameraDeb.transform.localRotation.eulerAngles}");
                    
                    
                   // GUILayout.Label($"Current time variation: {skyCameraDeb.transform.rotation.eulerAngles.y - Camera.main.transform.rotation.eulerAngles.y}");
                    

                    //GUILayout.Label($"Culling: {KerbalCullingManager.Singleton.gameObject.name}");
                    //GUILayout.Label($"Culling enabled: {KerbalCullingManager.Singleton.enabled}");
                    
                    //GUILayout.Label($"Camera pitch: {GameManager.Instance.Game.CameraManager.FlightCamera.ActiveSolution.GimbalState.pitch}");
                    //GUILayout.Label($"Camera roll: {GameManager.Instance.Game.CameraManager.FlightCamera.ActiveSolution.GimbalState.roll}");
                    //GUILayout.Label($"Camera pan: {GameManager.Instance.Game.CameraManager.FlightCamera.ActiveSolution.GimbalState.pan}");
                    //GUILayout.Label($"Camera localHeading: {GameManager.Instance.Game.CameraManager.FlightCamera.ActiveSolution.GimbalState.localHeading}");
                    //GUILayout.Label($"Camera localPitch: {GameManager.Instance.Game.CameraManager.FlightCamera.ActiveSolution.GimbalState.localPitch}");
                    //GUILayout.Label($"Unity camera : {Camera.main.transform.name}");
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

            //GUILayout.Label($"Status: {statusMsg}");
            //GUILayout.Label($"First Person Enabled: {isFirstPersonViewEnabled()}");
            //GUILayout.Label($"Main camera: {Camera.main.name}");
            //if (currentCamera) GUILayout.Label($"Current camera: {currentCamera.name}{" "}{currentCamera.enabled}");
            //if (kerbalCameraObject) GUILayout.Label($"My camera: {kerbalCameraObject.GetComponent<Camera>().name}{" "}{kerbalCameraObject.GetComponent<Camera>().enabled}");
            
            //GUILayout.BeginHorizontal();
            //GUILayout.Label("Heading Difference Ratio: ", GUILayout.Width(WINDOW_WIDTH / 2));
            //var headingDifferenceRatioString = GUILayout.TextField(headingDifferenceRatio.ToString());
            //double.TryParse(headingDifferenceRatioString, out headingDifferenceRatio);
            //GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Light intensity: ", GUILayout.Width(WINDOW_WIDTH / 2));
            var lightIntesityString = GUILayout.TextField(lightIntesity.ToString());
            float.TryParse(lightIntesityString, out lightIntesity);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("SpotAngle: ", GUILayout.Width(WINDOW_WIDTH / 2));
            var spotAngleString = GUILayout.TextField(spotAngle.ToString());
            float.TryParse(spotAngleString, out spotAngle);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Range: ", GUILayout.Width(WINDOW_WIDTH / 2));
            var rangeString = GUILayout.TextField(range.ToString());
            float.TryParse(rangeString, out range);
            GUILayout.EndHorizontal();



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

        private class OnDestroyHandler : MonoBehaviour {
            void OnDestroy() {
                var physicsCamera = GameObject.Find("FlightCameraPhysics_Main");
                physicsCamera.transform.parent = GameObject.Find("[PhysicsSpace] FlightCamera Assembly").transform;
                KerbalCullingManager.Singleton.enabled = true;
                physicsCamera.GetComponent<Camera>().enabled = true;
            }
        }
    }

/*
                    
      

                    if (kerbalBehavior) {
                Logger.Info("K " + kerbalBehavior.gameObject.name);
                if (kerbalBehavior.gameObject.transform.parent) Logger.Info("P " + kerbalBehavior.gameObject.transform.parent.name);
            } else {
                Logger.Info("No kerbal behavior");
            }
            if (Camera.main) {
                Logger.Info(Camera.main.name);
                if (Camera.main.transform.parent) Logger.Info("PC " + Camera.main.transform.parent.name);
            }
    

    */
}

