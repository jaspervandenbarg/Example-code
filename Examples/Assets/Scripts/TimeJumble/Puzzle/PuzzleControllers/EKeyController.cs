using UnityEngine;
using UnityEngine.EventSystems;
using UnityAtoms.BaseAtoms;

namespace Jigsar.Puzzles.Specific
{
    public class EKeyController : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private KeyType key;
        [SerializeField] private string number;
        [SerializeField] private StringVariable eCode;
        [SerializeField] private BoolVariable EKeyLocked;
        private UnderAttackPuzzleController controller;

        private void Start()
        {
            controller = GetComponentInParent<UnderAttackPuzzleController>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (EKeyLocked.Value) return;

            Debug.Log(number);
            switch (key)
            {
                case KeyType.Number:
                    eCode.Value += number;
                    break;
                case KeyType.Clear:
                    eCode.Value = "";
                    break;
                case KeyType.Enter:
                    if (controller != null) controller.AttemptCode();
                    break;
            }
        }

        private enum KeyType
        {
            Number,
            Clear,
            Enter
        }
    }
}



