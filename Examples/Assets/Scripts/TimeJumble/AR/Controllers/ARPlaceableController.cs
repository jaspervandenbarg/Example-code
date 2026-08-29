using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Sirenix.OdinInspector;

namespace Jigsar.AR
{
    public class ARPlaceableController : MonoBehaviour
    {
        [ShowInInspector, ReadOnly] private GameObject objectToPlace;
        public void SetObjectToPlace(GameObject value)
        {
            objectToPlace = value;
            // change this to already having the object ready when it gets set
            spawnedObject = Instantiate(objectToPlace, Vector3.zero, Quaternion.identity, this.transform);
            spawnedObject.SetActive(false);
            // use this as parent and then remove to make sure the object spawns in the correct scene
            spawnedObject.transform.parent = null;

        }

        private GameObject spawnedObject;
        [SerializeField] private GameObject placementIndicator;
        [SerializeField] private bool turnOffARPlaneManager;

        private Pose placementPose;
        private ARRaycastManager arRaycastManager;
        private ARPlaneManager arPlaneManager;
        private GameObject trackables;

        private bool placementPoseIsValid = false;

        public bool UseOcclusion { set => FindObjectOfType<AROcclusionManager>().enabled = value; }


        // Start is called before the first frame update
        void Awake()
        {
            arRaycastManager = FindObjectOfType<ARRaycastManager>();
            arPlaneManager = FindObjectOfType<ARPlaneManager>();
        }

        private void Start()
        {
            trackables = GameObject.Find("Trackables");
        }

        // Update is called once per frame
        void Update()
        {
            if (spawnedObject == null || (spawnedObject != null && !spawnedObject.activeSelf))
            {
                UpdatePlacementPose();
                UpdatePlacementIndicator();
            }
        }

        //has to work with buttons

        void UpdatePlacementIndicator()
        {
            if (spawnedObject == null || (spawnedObject != null && !spawnedObject.activeSelf))
            {
                placementIndicator.SetActive(true);
                placementIndicator.transform.SetPositionAndRotation(placementPose.position, placementPose.rotation);
            }
        }

        void UpdatePlacementPose()
        {
            Vector3 screenCenter = Camera.main.ViewportToScreenPoint(new Vector3(0.5f, 0.5f));
            List<ARRaycastHit> hits = new List<ARRaycastHit>();
            arRaycastManager.Raycast(screenCenter, hits, TrackableType.Planes);

            placementPoseIsValid = hits.Count > 0;
            if (placementPoseIsValid)
            {
                placementPose = hits[0].pose;
            }
        }

        public void ToggleARObject()
        {
            //if spawned object is null instantiate object
            if (spawnedObject == null)
            {
                // change this to already having the object ready when it gets set
                spawnedObject = Instantiate(objectToPlace, placementPose.position, placementPose.rotation, this.transform);
                // use this as parent and then remove to make sure the object spawns in the correct scene
                spawnedObject.transform.parent = null;

                if (turnOffARPlaneManager)
                {
                    arPlaneManager.enabled = false;
                    trackables.SetActive(false);
                }
                placementIndicator.SetActive(false);
            }
            //deactivate object
            else if (spawnedObject.activeSelf)
            {
                spawnedObject.SetActive(false);
                if (turnOffARPlaneManager)
                {
                    arPlaneManager.enabled = true;
                    trackables.SetActive(true);
                }
                placementIndicator.SetActive(true);

            }
            //reactivate at new position
            else if (!spawnedObject.activeSelf)
            {
                spawnedObject.transform.position = placementPose.position;
                spawnedObject.transform.rotation = placementPose.rotation;
                spawnedObject.SetActive(true);
                if (turnOffARPlaneManager)
                {
                    arPlaneManager.enabled = false;
                    trackables.SetActive(false);
                }
                placementIndicator.SetActive(false);
            }
        }
    }
}
