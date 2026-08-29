using UnityEngine;
using UnityEngine.EventSystems;
using Jigsar.Events;

namespace Jigsar.AR
{
    public class ARPlaceableMarker : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private VoidEvent placeObjectEvent;

        public void Awake()
        {
            Camera camera = Camera.main;
            PhysicsRaycaster caster = camera.GetComponent<PhysicsRaycaster>();

            if (caster == null) caster = camera.gameObject.AddComponent<PhysicsRaycaster>();

            caster.eventMask = LayerMask.GetMask("Selectable");
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            placeObjectEvent?.Raise();
        }
    }
}

