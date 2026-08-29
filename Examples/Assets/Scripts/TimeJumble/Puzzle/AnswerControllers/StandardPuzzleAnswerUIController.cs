using Jigsar.Puzzles;
using Jigsar.Events;
using TMPro;
using UnityEngine;

namespace Jigsar.UI
{
    public class StandardPuzzleAnswerUIController : BasePuzzleAnswerController
    {
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private BoolEvent ToggleAwnserButtonEvent;

        public override void TryAnswer()
        {
            answerToTry = inputField.text;

            base.TryAnswer();
        }

        protected override void CorrectAnswer()
        {
            base.CorrectAnswer();
            ToggleAwnserButtonEvent?.Raise(false);
            inputField.interactable = false;
        }
    }
}

