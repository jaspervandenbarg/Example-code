using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Jigsar.AR;

namespace Jigsar.Puzzles
{
    public class PlaceablePuzzleObject : BasePuzzleObject
    {
        [SerializeField] private GameObject objectToPlace;
        private ARPlaceableController placeableController;
        [SerializeField] private bool useOcclusion;

        protected override void Awake()
        {
            base.Awake();

            placeableController = FindObjectOfType<ARPlaceableController>();

            if (placeableController != null)
            {
                placeableController.SetObjectToPlace(objectToPlace);
                placeableController.UseOcclusion = useOcclusion;
            }
        }
    }
}

