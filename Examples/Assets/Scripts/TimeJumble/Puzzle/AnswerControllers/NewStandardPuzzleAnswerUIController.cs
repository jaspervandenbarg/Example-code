using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Jigsar.UI.Animations;

namespace Jigsar.UI
{
    public class NewStandardPuzzleAnswerUIController : StandardPuzzleAnswerUIController
    {
        [SerializeField] private UIPanelAnimation panelAnimation;
        public override void TryAnswer()
        {
            base.TryAnswer();
        }

        protected override void CorrectAnswer()
        {
            panelAnimation.TweenOut();
            base.CorrectAnswer();
        }
    }
}

