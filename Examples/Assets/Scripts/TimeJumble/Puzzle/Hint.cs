using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sirenix.OdinInspector;

namespace Jigsar.Puzzles
{
    [System.Serializable]
    public class Hint
    {
        private string hintString;
        [SerializeField] private bool hasImage;
        [ShowIf("hasImage")]
        private Image hintImage;

        public string HintString => hintString;
        public bool HasImage => hasImage;
        public Image HintImage => hintImage;
    }
}

