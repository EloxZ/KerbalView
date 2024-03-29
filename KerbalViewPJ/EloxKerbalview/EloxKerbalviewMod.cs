using UnityEngine;
using KSP;
using KSP.Game;
using SpaceWarp;
using SpaceWarp.UI;
using SpaceWarp.API.UI.Appbar;
using SpaceWarp.API.Mods;
using SpaceWarp.API.UI;
using BepInEx;
using KSP.Api.CoreTypes;
using System.Collections;

namespace EloxKerbalview
{   
    [BepInPlugin("com.Elox.EloxKerbalView", "EloxKerbalView", "1.1.0")]
    [BepInDependency(SpaceWarpPlugin.ModGuid, SpaceWarpPlugin.ModVer)]
    public class EloxKerbalviewMod : BaseSpaceWarpPlugin
    {
        private static EloxKerbalviewMod Instance { get; set; }
        static bool loaded = false;
        static bool firstPersonEnabled = false;

        KSP.Sim.impl.VesselComponent kerbal = null;
        KSP.Sim.impl.VesselBehavior kerbalVesselBehavior = null;
        KSP.Sim.impl.KerbalBehavior kerbalBehavior = null;
        GameObject kerbalCam = null;
        static int kerbalCamIndex = 0;

        Sprite telescopeSprite;
        Texture2D telescopeTexture;
        float lastKerbalYRotation;

        Camera currentCamera;
        Camera skyCamera;
        Camera scaledCamera;
        Vector3 savedPosition;
        Quaternion savedRotation;
        Transform savedParent;

        static float cameraNearClipPlane = 20;
        static float cameraFOV = 90;
        static float cameraForwardOffset = 170;
        static float cameraUpOffset = 70;

        float savedFov;
        float savedNearClip;

        static GameObject helmetLights, telescopeSight;
        KSP.Sim.Definitions.ModuleAction toggleLightsAction;
        static float range = 20, spotAngle = 45, lightIntesity = 100;
        static bool cameraLocked = false;
        static float currentCameraPitch = 0, maxCameraPitch = 40, currentCameraYaw = 0, maxCameraYaw = 40;
        static int telescopeMode = 0;
        static float sensitivity = 1;
        static KSP.Sim.Definitions.ModuleProperty<Color> helmetLightsColorProperty;
        Color helmetLightsColor = Color.white;

        GameObject cameraParent;

        public override void OnInitialized() {
            Logger.LogInfo("KerbalView is initialized");
            telescopeTexture = SpaceWarp.API.Assets.AssetManager.GetAsset<Texture2D>("EloxKerbalView/images/telescopeMask.png");
            telescopeSprite = Sprite.Create(telescopeTexture, new Rect(0.0f, 0.0f, telescopeTexture.width, telescopeTexture.height), new Vector2(0.5f, 0.5f));
            toggleLightsAction = new KSP.Sim.Definitions.ModuleAction(toggleHelmetLights);
            helmetLightsColorProperty = new KSP.Sim.Definitions.ModuleProperty<Color>(helmetLightsColor);

            if (loaded) {
                Destroy(this);
            }
        }

        public override void OnPostInitialized()
        {
            loaded = true; 
        }

        void Start() {
            firstPersonEnabled = false;
            telescopeMode = 0;
            cameraLocked = false;
        }

        void Update() {
            if (loaded) {
                try {
                    if (GameManager.Instance.Game.GlobalGameState.GetGameState().IsFlightMode) {
                        if (kerbalVesselBehavior != null || kerbalCam != null) {
                            if (isFirstPersonViewEnabled() && gameChangedCamera()) disableFirstPerson();
                            if (isFirstPersonViewEnabled()) {
                                if (kerbalBehavior != null) {
                                    kerbalBehavior.EVAAnimationManager.Animator.SetFloat(Animator.StringToHash("iEmote"), 0);
                                    kerbalBehavior.EVAAnimationManager.Animator.SetFloat(Animator.StringToHash("fRandomIdle"), 0);
                                    kerbalBehavior.EVAAnimationManager.Animator.SetFloat(Animator.StringToHash("tFidget"), 0);
                                }

                                if (kerbalCam != null && Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.Alpha3)) nextKerbalCam();
                                if (Input.GetKeyDown(KeyCode.Mouse1) || cameraLocked && Input.GetKeyDown(KeyCode.M)) toggleLockCamera();
                                if (cameraLocked) handleCameraMovement();

                                updateStars();
                            }

                            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.L)) toggleHelmetLights();
                            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.Alpha2) && GameManager.Instance.Game.CameraManager.FlightCamera.Mode == KSP.Sim.CameraMode.Auto) {
                                if (!isFirstPersonViewEnabled()) {
                                    enableFirstPerson();
                                } else {
                                    disableFirstPerson();
                                }
                            }
                        } else {
                            if (!findKerbal()) findKerbalCam();
                        }
                    }
                
                } catch (Exception e)
                {
                    Logger.LogError(e.StackTrace);
                }
            }
        }

        void findKerbalCam() {
            kerbalCamIndex = 0;
            foreach (Camera c in Camera.allCameras) {
                if (c.gameObject.name == "kerbalCam" && c.gameObject.transform.parent.FindChildRecursive("bone_kerbal_master_parent")) {
                    kerbalCam = c.gameObject;
                    break;
                }
            }
        }

        void nextKerbalCam() {
            List<GameObject> kerbalCams = new List<GameObject>();
            foreach (Camera c in Camera.allCameras) {
                if (c.gameObject.name == "kerbalCam" && c.gameObject.transform.parent.FindChildRecursive("bone_kerbal_master_parent")) {
                    kerbalCams.Add(c.gameObject);
                }
            }
            if (kerbalCams.Count > 1) {
                kerbalCamIndex++;
                kerbalCamIndex %= kerbalCams.Count;
            
                try {
                    kerbalCam.transform.parent.FindChildRecursive("bone_kerbal_master_parent").parent.localScale = Vector3.one;
                    kerbalCam = kerbalCams[kerbalCamIndex];
                    kerbalCam.transform.parent.FindChildRecursive("bone_kerbal_master_parent").parent.localScale = Vector3.zero;
                    cameraParent.transform.SetParent(kerbalCam.transform, false);
                } catch (Exception e) {

                }
            }

        }

        void toggleScopeSight() {
            if (telescopeSight) {
                Destroy(telescopeSight);
                if (!GameManager.Instance.Game.UI.FlightHud.IsVisible) GameManager.Instance.Game.UI.FlightHud.ToggleFlightHUD();
            } else {
                telescopeSight = new GameObject("TelescopeSight");
                var rectTransform = telescopeSight.AddComponent<RectTransform>();
                var image = telescopeSight.AddComponent<UnityEngine.UI.Image>();

                rectTransform.anchorMin = new Vector2(0, 0);
                rectTransform.anchorMax = new Vector2(1, 1);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                if (GameManager.Instance.Game.UI.FlightHud.IsVisible) GameManager.Instance.Game.UI.FlightHud.ToggleFlightHUD();
                image.sprite = telescopeSprite;

                telescopeSight.transform.SetParent(GameObject.Find("Canvas")?.transform, false);
            }
        }

        void setTelescope(int mode) {
            if (mode == 0) {
                GameManager.Instance.Game.CameraManager.FlightCamera.ActiveSolution.SetCameraFieldOfView(cameraFOV);
                GameManager.Instance.Game.CameraManager.FlightCamera.Tweakables.minFOV = 30;
                telescopeMode = 0;
                sensitivity = 1;
                if (telescopeSight) toggleScopeSight();
            } else if (mode == 1) {
                GameManager.Instance.Game.CameraManager.FlightCamera.Tweakables.minFOV = 1;
                GameManager.Instance.Game.CameraManager.FlightCamera.ActiveSolution.SetCameraFieldOfView(cameraFOV/4);
                telescopeMode = 1;
                sensitivity = 0.5f;
                if (!telescopeSight) toggleScopeSight();
            }  else if (mode == 2) {
                GameManager.Instance.Game.CameraManager.FlightCamera.Tweakables.minFOV = 1;
                GameManager.Instance.Game.CameraManager.FlightCamera.ActiveSolution.SetCameraFieldOfView(cameraFOV/20);
                telescopeMode = 2;
                sensitivity = 0.1f;
                if (!telescopeSight) toggleScopeSight();
            } else if (mode == 3) {
                GameManager.Instance.Game.CameraManager.FlightCamera.Tweakables.minFOV = 1;
                GameManager.Instance.Game.CameraManager.FlightCamera.ActiveSolution.SetCameraFieldOfView(cameraFOV/80);
                telescopeMode = 3;
                sensitivity = 1/40f;
                if (!telescopeSight) toggleScopeSight();
            }
        }

        void handleCameraMovement() {
            var movementX = Input.GetAxis("Mouse X");
            var movementY = -Input.GetAxis("Mouse Y");

            if (Input.GetAxis("Mouse ScrollWheel") < 0f) setTelescope(telescopeMode-1);
            if (Input.GetAxis("Mouse ScrollWheel") > 0f) setTelescope(telescopeMode+1);

            currentCameraYaw += movementX*sensitivity;
            currentCameraPitch += movementY*sensitivity;
        
            if (telescopeMode > 0) {
                if (currentCameraYaw > maxCameraYaw-10) currentCameraYaw = maxCameraYaw-10;
                if (currentCameraYaw < -maxCameraYaw-10) currentCameraYaw = -maxCameraYaw-10;
                if (currentCameraPitch > maxCameraPitch-10) currentCameraPitch = maxCameraPitch-10;
                if (currentCameraPitch < -maxCameraPitch-10) currentCameraPitch = -maxCameraPitch-10;
            } else {
                if (currentCameraYaw > maxCameraYaw) currentCameraYaw = maxCameraYaw;
                if (currentCameraYaw < -maxCameraYaw) currentCameraYaw = -maxCameraYaw;
                if (currentCameraPitch > maxCameraPitch) currentCameraPitch = maxCameraPitch;
                if (currentCameraPitch < -maxCameraPitch) currentCameraPitch = -maxCameraPitch;
            }

            currentCamera.transform.localEulerAngles = new Vector3(currentCameraPitch, currentCameraYaw, 0);
        }

        void lockCamera() {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            currentCameraYaw = 0;
            currentCameraPitch = 0;
            currentCamera.transform.localEulerAngles = new Vector3(0, 0, 0);
            cameraLocked = true;
            GameManager.Instance.Game.InputManager.SetInputLock(KSP.Input.InputLocks.EVAInputDisabled);
        }

        void unlockCamera() {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            currentCameraYaw = 0;
            currentCameraPitch = 0;
            cameraLocked = false;
            setTelescope(0);
            GameManager.Instance.Game.InputManager.SetInputLock(KSP.Input.InputLocks.EVAInputEnabled);
        }

        void toggleLockCamera() {
            if (cameraLocked) {
                unlockCamera();
                currentCamera.transform.localEulerAngles = new Vector3(0, 0, 0);
            } else {
                lockCamera();
            }
        }


        void toggleHelmetLights() {
            if (helmetLights) {
                Destroy(helmetLights);
            } else if (kerbalVesselBehavior) {
                helmetLights = new GameObject("EVA_HelmetLight");
                GameObject helmetLightLeft = new GameObject("EVA_HelmetLightLeft");
                GameObject helmetLightRight = new GameObject("EVA_HelmetLightRight");

                helmetLights.transform.parent = kerbalVesselBehavior.transform;
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
            var movement = currentCamera.transform.rotation.eulerAngles.y - lastKerbalYRotation ;
            lastKerbalYRotation = currentCamera.transform.rotation.eulerAngles.y;

            var targetY = skyCamera.transform.eulerAngles.y + movement;
            
            skyCamera.transform.eulerAngles = new Vector3(currentCamera.transform.eulerAngles.x, targetY, currentCamera.transform.eulerAngles.z);
            scaledCamera.transform.eulerAngles = new Vector3(currentCamera.transform.eulerAngles.x, currentCamera.transform.eulerAngles.y, currentCamera.transform.eulerAngles.z);
        }

        bool gameChangedCamera() {
            return currentCamera != Camera.main || currentCamera.enabled == false || GameManager.Instance.Game.CameraManager.FlightCamera.Mode != KSP.Sim.CameraMode.Auto || kerbalVesselBehavior != null && GameManager.Instance.Game.ViewController.GetActiveSimVessel() != kerbal || kerbalCam != null && findKerbal() || cameraParent == null;
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

                if (!cameraParent) cameraParent = new GameObject();

                // Save config
                savedParent = currentCamera.transform.parent;
                savedRotation = currentCamera.transform.localRotation;
                savedPosition = currentCamera.transform.localPosition;

                savedFov = currentCamera.fieldOfView;
                savedNearClip = currentCamera.nearClipPlane;

                // Camera config
                GameManager.Instance.Game.CameraManager.FlightCamera.ActiveSolution.SetCameraFieldOfView(cameraFOV);
                currentCamera.nearClipPlane = 0.001f*cameraNearClipPlane;

                // Current sky deviation caused by time
                var time = skyCamera.transform.eulerAngles.y - currentCamera.transform.eulerAngles.y;

                // Anchor camera to our little friend
                if (kerbalVesselBehavior) {
                    cameraParent.transform.parent = kerbalVesselBehavior.transform.parent.GetComponentInChildren<Kerbal3DModel>().transform;
                } else {
                    cameraParent.transform.parent = kerbalCam.transform;
                }
                cameraParent.transform.parent.gameObject.AddComponent<OnDestroyHandler>();
                currentCamera.transform.parent = cameraParent.transform;

                if (kerbalVesselBehavior) {
                    cameraParent.transform.localRotation = Quaternion.identity;
                } else {
                    cameraParent.transform.localEulerAngles = new Vector3(0, 180, 0);
                }

                currentCamera.transform.localRotation = Quaternion.identity;


                var targetPosition = (kerbalVesselBehavior) ? kerbalVesselBehavior.transform.position + 0.001f * cameraUpOffset * kerbalVesselBehavior.transform.up + 0.001f * cameraForwardOffset * kerbalVesselBehavior.transform.forward
                    : kerbalCam.transform.position + 0.001f * cameraUpOffset * kerbalCam.transform.up + 0.003f * cameraForwardOffset * kerbalCam.transform.forward;
                cameraParent.transform.position = targetPosition;
                currentCamera.transform.position = targetPosition;

                // Hide kerbal model
                try {
                    if (kerbalVesselBehavior) {
                        kerbalVesselBehavior.transform.parent.FindChildRecursive("bone_kerbal_master_parent").parent.localScale = Vector3.zero;
                    } else {
                        kerbalCam.transform.parent.FindChildRecursive("bone_kerbal_master_parent").parent.localScale = Vector3.zero;
                    }
                } catch (Exception e) {

                }

                // Remove camera collision
                //try {
                //currentCamera.gameObject.GetComponent<Rigidbody>().isKinematic = false;
                //} catch (Exception e) {
                //}

                // Sync cameras and desync by time
                skyCamera.transform.rotation = currentCamera.transform.rotation;
                scaledCamera.transform.rotation = currentCamera.transform.rotation;
                skyCamera.transform.eulerAngles += new Vector3(0,time,0);

                lastKerbalYRotation = currentCamera.transform.rotation.eulerAngles.y;

                //GameManager.Instance.Game.InputManager.SetInputLock(KSP.Input.InputLocks.EVAInputDisabled);

                firstPersonEnabled = true;
            } catch (Exception exception) {
                // For unknown error cases
                Logger.LogError(exception.ToString());
                GameManager.Instance.Game.CameraManager.EnableInput();
            }

            
        }

        void disableFirstPerson() {
            // Enable kerbal model
            try {
                if (kerbalVesselBehavior) {
                    kerbalVesselBehavior.transform.parent.FindChildRecursive("bone_kerbal_master_parent").parent.localScale = Vector3.one;
                } else {
                    kerbalCam.transform.parent.FindChildRecursive("bone_kerbal_master_parent").parent.localScale = Vector3.one;
                }
            } catch (Exception e) {

            }
            // To avoid NullRefs
            if (currentCamera && skyCamera && scaledCamera) {
                var time = skyCamera.transform.eulerAngles.y - currentCamera.transform.eulerAngles.y;
                
                // Revert changes
                currentCamera.transform.parent = savedParent;
                currentCamera.transform.localPosition = savedPosition;
                currentCamera.transform.localRotation = savedRotation;
                Destroy(cameraParent);

                // Sync cameras and desync by time
                skyCamera.transform.rotation = currentCamera.transform.rotation;
                scaledCamera.transform.rotation = currentCamera.transform.rotation;
                skyCamera.transform.eulerAngles += new Vector3(0,time,0);

                // Reset local rotations (tends to variate a bit with movement)
                skyCamera.transform.localRotation = Quaternion.identity;
                scaledCamera.transform.localRotation = Quaternion.identity;

                unlockCamera();

                GameManager.Instance.Game.CameraManager.FlightCamera.Tweakables.minFOV = 30;
                GameManager.Instance.Game.CameraManager.FlightCamera.ActiveSolution.SetCameraFieldOfView(savedFov);
                currentCamera.nearClipPlane = savedNearClip;

                if (Camera.main == null) currentCamera.enabled = true;
            }

            GameManager.Instance.Game.CameraManager.EnableInput();

            kerbal = null;
            kerbalVesselBehavior = null;
            kerbalBehavior = null;
            kerbalCam = null;

            firstPersonEnabled = false;
        }

        bool isFirstPersonViewEnabled() {
            return firstPersonEnabled;
        }

        bool findKerbal() {
            var activeVessel = GameManager.Instance.Game.ViewController.GetActiveSimVessel();
            kerbal = (activeVessel != null && activeVessel.IsKerbalEVA)? activeVessel : null;
            if (kerbal != null) {
                kerbalVesselBehavior = GameManager.Instance.Game.ViewController.GetBehaviorIfLoaded(kerbal);
                try
                {
                    kerbal.SimulationObject.Kerbal.KerbalData.AddAction("Toggle Helmet Lights", toggleLightsAction); 
                    kerbal.SimulationObject.Kerbal.KerbalData.AddProperty("helmetLightsColor", helmetLightsColorProperty); // Doesn't work :(
                    
                }
                catch (Exception exception)
                {
                    Logger.LogInfo(exception.ToString());
                }
                
                kerbalBehavior = GameManager.Instance.Game.ViewController.GetBehaviorIfLoaded(kerbal.SimulationObject.Kerbal);
            }
            return kerbalVesselBehavior != null;
        }

        class OnDestroyHandler : MonoBehaviour {
            void OnDestroy() {
                Instance.cameraParent.transform.parent = null;
                foreach (Camera c in Camera.allCameras) {
                    if (c.gameObject.name == "FlightCameraPhysics_Main") {
                        c.enabled = true;
                        break;
                    }
                }
            }
        }
    }
}

