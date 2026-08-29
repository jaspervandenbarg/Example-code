using System.Collections;
using System.Collections.Generic;
using UnityAtoms.BaseAtoms;
using UnityEngine;
using UnityEngine.EventSystems;
using Sirenix.OdinInspector;

namespace Jigsar.AR
{
    public class ARKeyController : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private KeyType key;
        private bool isNumber => key == KeyType.Number;
        [ShowIf("isNumber")]
        [SerializeField] private string number;
        private ARKeypadController controller;
        private bool isHint => key == KeyType.Hint;
        [ShowIf("isHint")]
        [SerializeField] private bool hintOnStart = false;

        private void Start()
        {
            controller = GetComponentInParent<ARKeypadController>();
            if (hintOnStart) controller.ShowHint();

            if(key == KeyType.Clear) controller.ClearCode();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            switch (key)
            {
                case KeyType.Number:
                    controller.AddToCode(number);
                    break;
                case KeyType.Clear:
                    controller.ClearCode();
                    break;
                case KeyType.Enter:
                    controller.AttemptCode();
                    break;
                case KeyType.Hint:
                    controller.ShowHint();
                    break;
            }
        }

        private enum KeyType
        {
            Number,
            Clear,
            Enter,
            Hint
        }
    }
}

