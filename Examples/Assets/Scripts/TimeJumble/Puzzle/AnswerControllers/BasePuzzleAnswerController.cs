using UnityEngine;
using UnityEngine.Localization;
using Jigsar.Location;
using Jigsar.Events;
using Jigsar.Audio;

namespace Jigsar.Puzzles
{
    public class BasePuzzleAnswerController : MonoBehaviour
    {
        //[SerializeField] protected StringVariable currentPuzzleAnswer;
        [SerializeField] protected CurrentLocation currentLocation;
        [SerializeField] protected LocalizedStringEvent localizedStringEvent;
        //[SerializeField] protected LocalizedString correctAnswer, wrongAnswer;
        [SerializeField] private AudioElement audioCorrect, audioWrong;
        

        protected string answerToTry;

        protected bool ValidAnswer(string value)
        {
            if (currentLocation.Puzzle.Answer.Contains("^&*")) return MultipleSolutions(value);
            else return SingleSolution(value);
        }

        protected bool SingleSolution(string value)
        {
            if (currentLocation.Puzzle.Answer.Equals(value, System.StringComparison.OrdinalIgnoreCase)) return true;
            else return false;
        }
        protected bool MultipleSolutions(string value)
        {
            string[] possibilities = currentLocation.Puzzle.Answer.Split(new string[] { "^&*" }, System.StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < possibilities.Length; i++)
            {
                if (value.Equals(possibilities[i], System.StringComparison.OrdinalIgnoreCase)) return true;
                else continue;
            }

            return false;
        }

        public virtual void TryAnswer()
        {
            if (currentLocation.Puzzle.Completed) return;

            if (ValidAnswer(answerToTry))
                CorrectAnswer();
            else
                WrongAnswer();
        }

        protected virtual void CorrectAnswer()
        {
            currentLocation.Puzzle.UpdateTimeSpend();
            currentLocation.Puzzle.Complete();
            localizedStringEvent.Raise(currentLocation.Puzzle.ResponseP);
            UIAudioManager.Instance?.PlayOnSource(audioCorrect.Clip, audioCorrect.Volume, audioCorrect.Pitch);
        }

        protected virtual void WrongAnswer()
        {
            localizedStringEvent.Raise(currentLocation.Puzzle.ResponseN);
            UIAudioManager.Instance?.PlayOnSource(audioWrong.Clip, audioWrong.Volume, audioWrong.Pitch);
        }


    }
}

