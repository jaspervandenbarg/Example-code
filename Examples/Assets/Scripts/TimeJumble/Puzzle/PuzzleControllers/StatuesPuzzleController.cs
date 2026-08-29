using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Jigsar.Puzzles.Specific
{
    [RequireComponent(typeof(MeshCollider))]
    public class StatuesPuzzleController : BasePuzzleAnswerController, IPointerClickHandler
    {
        [SerializeField] private string statueNumber;

        private void Start()
        {
            Camera camera = Camera.main;
            PhysicsRaycaster caster = camera.GetComponent<PhysicsRaycaster>();

            if (caster == null) caster = camera.gameObject.AddComponent<PhysicsRaycaster>();

            caster.eventMask = LayerMask.GetMask("Selectable");
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            answerToTry = statueNumber;
            TryAnswer();
        }
    }
}

