using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Jigsar.Puzzles;
using Jigsar.Location;
using UnityAtoms.BaseAtoms;
using TMPro;
using UnityEngine.EventSystems;
using Jigsar.Events;
using UnityEngine.Playables;
using Jigsar.Audio;

namespace Jigsar.AR
{
    public class ARKeypadController : MonoBehaviour
    {
        [HorizontalGroup("group1")]
        [SerializeField] private PuzzleType puzzleType;
        bool _generalPuzzle => puzzleType == PuzzleType.NonSpecific ? true : false;

        [HorizontalGroup("group1"), ShowIf("_generalPuzzle")]
        [SerializeField, HideLabel] private CurrentLocation currentLocation;
        [HorizontalGroup("group1"), HideIf("_generalPuzzle")]
        [SerializeField, HideLabel] private PuzzleLocation specificPuzzle;

        [SerializeField] private CurrentLocation selectedPuzzle;
        private string code;
        private bool attemptingCode;
        [SerializeField] private int flashes;
        [SerializeField] private float flashSpeed;
        private bool softLocked, hardLocked;
        [SerializeField] private Canvas canvas;
        private TMP_Text text;
        [SerializeField] private Jigsar.Events.VoidEvent solvedEvent;
        [SerializeField] private Jigsar.Events.IntEvent togglePanelEvent;

        [SerializeField] private AudioElement audioCorrect, audioWrong;

        private PuzzleLocation puzzleToUse;

        private void Awake()
        {
            softLocked = false;
            hardLocked = false;

            Camera camera = Camera.main;
            PhysicsRaycaster caster = camera.GetComponent<PhysicsRaycaster>();

            if (caster == null) caster = camera.gameObject.AddComponent<PhysicsRaycaster>();

            caster.eventMask = LayerMask.GetMask(new string[] { "Selectable", "Default" });

            if (canvas != null)
            {
                canvas.sortingOrder = -2;
                canvas.worldCamera = camera;
                text = canvas.GetComponentInChildren<TMP_Text>();
                text.text = "";
            }

            puzzleToUse = _generalPuzzle ? currentLocation.Puzzle : specificPuzzle;
        }

        private void OnEnable()
        {
            puzzleToUse.SetStartTime();
        }
        private void OnDisable()
        {
            if (!puzzleToUse.Completed) puzzleToUse.UpdateTimeSpend();
        }

        public void ClearCode()
        {
            code = "";
            text.text = code;
        }
        public void AddToCode(string value)
        {
            if (!softLocked && !hardLocked) code += value;
            text.text = code;
        }
        // do this functionality
        public void ShowHint()
        {
            if (puzzleToUse == null) return;
            selectedPuzzle.Location = puzzleToUse;
            togglePanelEvent?.Raise(9);
        }

        public void AttemptCode()
        {
            if (puzzleToUse == null)
            {
                Debug.LogWarning("There are no puzzles selected but I will allow any answer.");
                StartCoroutine(ScreenResponse("Null"));
                //solvedEvent.Raise();
                code = "";
                return;
            }

            if (ValidAnswer(puzzleToUse.Answer, code))
                CorrectAnswer(puzzleToUse);
            else
                WrongAnswer(puzzleToUse);

            code = "";
        }

        private void CorrectAnswer(PuzzleLocation puzzle)
        {
            puzzle.UpdateTimeSpend();
            puzzle.Complete();
            hardLocked = true;
            //localizedStringEvent?.Raise(puzzle.ResponseP);
            solvedEvent?.Raise();
            StartCoroutine(ScreenResponse("Correct"));
            UIAudioManager.Instance?.Play(audioCorrect.Clip, audioCorrect.Volume, audioCorrect.Pitch);
        }
        private void WrongAnswer(PuzzleLocation puzzle)
        {
            //localizedStringEvent?.Raise(puzzle.ResponseN);
            StartCoroutine(ScreenResponse("Wrong"));
            UIAudioManager.Instance?.Play(audioWrong.Clip, audioWrong.Volume, audioWrong.Pitch);
        }

        protected bool ValidAnswer(string a, string b)
        {
            if (a.Contains("^&*")) return MultipleSolutions(a, b);
            else if (a.Equals("YEAR")) return YearSolution(b);
            else return SingleSolution(a, b);
        }

        protected bool SingleSolution(string a, string b)
        {
            if (a.Equals(b, System.StringComparison.OrdinalIgnoreCase)) return true;
            else return false;
        }
        protected bool MultipleSolutions(string a, string b)
        {
            string[] possibilities = a.Split(new string[] { "^&*" }, System.StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < possibilities.Length; i++)
            {
                if (b.Equals(possibilities[i], System.StringComparison.OrdinalIgnoreCase)) return true;
                else continue;
            }

            return false;
        }
        protected bool YearSolution(string input)
        {
            if (input.Equals(System.DateTime.Today.Year.ToString())) return true;
            else return false;
        }

        private enum PuzzleType
        {
            Specific,
            NonSpecific,
        }

        private IEnumerator ScreenResponse(string response)
        {
            if (attemptingCode) yield break;
            if (canvas == null) yield break;

            softLocked = true;
            attemptingCode = true;

            if (flashes % 2 == 1) flashes++;

            for (int i = 0; i < flashes; i++)
            {
                if (i % 2 == 0) text.text = response;
                else text.text = "";
                yield return new WaitForSeconds(flashSpeed);
            }
            softLocked = false;
            attemptingCode = false;

            //do something if code is correct;
            if (hardLocked)
            {
                text.text = "solved";
                //solvedEvent?.Raise();
                //animator.SetTrigger("Close");
                //director?.Play();
            }
        }
    }
}

