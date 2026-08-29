using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Jigsar.AR;

namespace Jigsar.Puzzles
{
    public class ImageTrackingPuzzleObject : BasePuzzleObject
    {
        [SerializeField] private XRReferenceImageLibrary imageLibrary;
        [Tooltip("Names of the prefabs must match the names of the images they need to be placed on!")]
        [SerializeField] private GameObject[] imageObjects = new GameObject[0];

        private ARImageTrackingController imageTrackingController;

        protected override void Start()
        {
            base.Start();

            imageTrackingController = FindObjectOfType<ARImageTrackingController>();

            if (imageTrackingController != null)
                imageTrackingController.InitializeImageTracking(imageLibrary, imageObjects);
        }
    }
}

