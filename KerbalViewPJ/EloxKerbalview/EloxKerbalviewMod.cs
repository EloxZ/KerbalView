using UnityEngine;
using SpaceWarp.API.Mods;
using KSP.Game;

namespace EloxKerbalview
{
    [MainMod]
    public class EloxKerbalviewMod : Mod
    {
        static bool loaded = false;
        static bool firstPersonEnabled = false;

        KSP.Sim.impl.VesselComponent kerbal = null;
        KSP.Sim.impl.VesselBehavior kerbalBehavior = null;
        float lastKerbalYRotation;

        Camera currentCamera;
        Camera skyCamera;
        Camera scaledCamera;
        Vector3 savedPosition;
        Quaternion savedRotation;
        Transform savedParent;

        static float cameraNearClipPlane = 8;
        static float cameraFOV = 90;
        static float cameraForwardOffset = 10;
        static float cameraUpOffset = 12;

        float savedFov;
        float savedNearClip;

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

        void Update() {
            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.Alpha2) && findKerbal() && GameManager.Instance.Game.CameraManager.FlightCamera.Mode == KSP.Sim.CameraMode.Auto) {
                if (!isFirstPersonViewEnabled()) {
                    enableFirstPerson();
                } else {
                    disableFirstPerson();
                }
            }

            if (isFirstPersonViewEnabled() && gameChangedCamera()) disableFirstPerson();
            if (isFirstPersonViewEnabled()) updateStars();
        }

        void updateStars() {
            var movement = currentCamera.transform.rotation.eulerAngles.y - lastKerbalYRotation;
            lastKerbalYRotation = currentCamera.transform.rotation.eulerAngles.y;

            var targetY = skyCamera.transform.eulerAngles.y + movement;
            
            skyCamera.transform.eulerAngles = new Vector3(currentCamera.transform.eulerAngles.x, targetY, currentCamera.transform.eulerAngles.z);
            scaledCamera.transform.eulerAngles = new Vector3(currentCamera.transform.eulerAngles.x, targetY, currentCamera.transform.eulerAngles.z);
        }

        bool gameChangedCamera() {
            return currentCamera != Camera.main || GameManager.Instance.Game.CameraManager.FlightCamera.Mode != KSP.Sim.CameraMode.Auto || kerbal == null;
            
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
            kerbalBehavior = Game.ViewController.GetBehaviorIfLoaded(kerbal);
            return kerbal != null && kerbalBehavior != null;
        }
    }
}

