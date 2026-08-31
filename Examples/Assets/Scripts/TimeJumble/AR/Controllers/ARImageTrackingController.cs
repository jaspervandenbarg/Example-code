using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Sirenix.OdinInspector;
using Sirenix.Serialization;

namespace Jigsar.AR
{
    /// <summary>
    /// Able to track multiple images at a time
    /// </summary>
    public class ARImageTrackingController : SerializedMonoBehaviour
    {
        private ARTrackedImageManager trackedImageManager;

        [OdinSerialize] private XRReferenceImageLibrary imageLibrary;

        // Review 31-08-2026: did not get to this point in the past because this was safe for our small controlled environment but needs updating for larger projects.
        // might change this to a custom object with string:name and gameobject:prefab
        [Tooltip("Be sure the names of the prefabs match with the names in the image library! Prefabs must each have a unique name")]
        [SerializeField] private GameObject[] objectsToPlace = new GameObject[0];
        [SerializeField] private float scale;
        [ShowInInspector, ReadOnly] private Dictionary<string, GameObject> spanwedObjects = new Dictionary<string, GameObject>();

        [SerializeField] private GameObject anchor;

        public void InitializeImageTracking(XRReferenceImageLibrary library, GameObject[] objects)
        {
            // Review 31-08-2026: could have build a dictionary once and looked for key instead of looping over list each update in UpdateTrackedImage
            imageLibrary = library;

            objectsToPlace = objects;
            InstantiateObjects();

            //disbale then add library
            trackedImageManager.enabled = false;

            trackedImageManager.maxNumberOfMovingImages = imageLibrary.count;      //test
            trackedImageManager.referenceLibrary = imageLibrary;
            //enable to make it use the new library
            trackedImageManager.enabled = true;
        }

        private void Awake()
        {
            trackedImageManager = GetComponent<ARTrackedImageManager>();
        }

        private void OnEnable()
        {
            trackedImageManager.trackedImagesChanged += TrackedImageManager_trackedImagesChanged;
        }

        private void OnDisable()
        {
            trackedImageManager.trackedImagesChanged -= TrackedImageManager_trackedImagesChanged;
        }

        private void TrackedImageManager_trackedImagesChanged(ARTrackedImagesChangedEventArgs obj)
        {
            //Set position, rotation and visibility
            foreach (ARTrackedImage image in obj.added)
            {
                spanwedObjects[image.name].SetActive(true);
            }

            foreach (ARTrackedImage image in obj.updated)
            {
                UpdateTrackedImage(image);
            }

            foreach (ARTrackedImage image in obj.removed)
            {
                spanwedObjects[image.name].SetActive(false);
            }
        }

        private void UpdateTrackedImage(ARTrackedImage image)
        {
            for (int i = 0; i < imageLibrary.count; i++)
            {
                if (image.referenceImage.guid == imageLibrary[i].guid)
                {
                    string imageName = imageLibrary[i].name;
                    if (image.trackingState == TrackingState.Tracking)
                    {
                        if (spanwedObjects.ContainsKey(imageName))
                        {
                            spanwedObjects[imageName].SetActive(true);
                            spanwedObjects[imageName].transform.SetPositionAndRotation(image.transform.position, image.transform.rotation);
                        }
                        else continue;
                    }
                    else
                    {
                        if (spanwedObjects.ContainsKey(imageName))
                            spanwedObjects[imageName].SetActive(false);
                    }
                }
                else continue;
            }
        }

        private void InstantiateObjects()
        {
            //anchor = new GameObject("Trackables");

            for (int i = 0; i < objectsToPlace.Length; i++)
            {
                GameObject tempObject = Instantiate(objectsToPlace[i], Vector3.zero, Quaternion.identity, anchor.transform);
                //tempObject.transform.localScale = new Vector3(scale, scale, scale); // test
                tempObject.name = objectsToPlace[i].name;
                tempObject.SetActive(false);
                spanwedObjects.Add(tempObject.name, tempObject);
            }
        }
    }
}

