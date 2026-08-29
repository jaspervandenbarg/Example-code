using System.Collections;
using UnityAtoms.BaseAtoms;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Jigsar.ScriptableObjects;

namespace Jigsar.Puzzles.Specific
{
    public class UnderAttackPuzzleController : BasePuzzleAnswerController
    {
        [SerializeField] private StringVariable eCode;
        [SerializeField] private BoolVariable EKeyLocked;
        private bool attemptingCode;
        [Range(5, 10)]
        [SerializeField] private int flashes;
        [SerializeField] private float flashSpeed = 0.2f;
        [SerializeField] private Canvas canvas;

        [SerializeField] private CurrentScene currentScene;
        [SerializeField] private Jigsar.Events.LoadSceneEvent loadSceneEvent;

        public void AttemptCode() => TryAnswer();

        private Animator animator;

        private void Awake()
        {
            EKeyLocked.Value = false;

            animator = GetComponent<Animator>();
        }

        private void OnEnable()
        {
            animator.SetBool("Attacking", !currentLocation.Puzzle.Completed);
        }
        private void OnDisable()
        {
            eCode.Value = "";
        }

        private void Start()
        {
            Camera camera = Camera.main;
            PhysicsRaycaster caster = camera.GetComponent<PhysicsRaycaster>();

            if (caster == null) caster = camera.gameObject.AddComponent<PhysicsRaycaster>();

            caster.eventMask = LayerMask.GetMask("Selectable");

            canvas.sortingOrder = -2;
            canvas.worldCamera = camera;
        }

        public override void TryAnswer()
        {
            answerToTry = eCode.Value;

            base.TryAnswer();
        }

        protected override void CorrectAnswer()
        {
            base.CorrectAnswer();

            animator.SetBool("Attacking", false);

            StartCoroutine(ScreenResponse("Correct"));

            EKeyLocked.Value = true;
        }

        protected override void WrongAnswer()
        {
            base.WrongAnswer();

            StartCoroutine(ScreenResponse("Wrong"));
        }

        private IEnumerator ScreenResponse(string response)
        {
            if (attemptingCode) yield break;

            attemptingCode = true;

            if (flashes % 2 == 1) flashes++;

            for (int i = 0; i < flashes; i++)
            {
                if (i % 2 == 0) eCode.Value = response;
                else eCode.Value = "";
                yield return new WaitForSeconds(flashSpeed);
            }
            attemptingCode = false;
        }

        public void CompletedButNotSolved()
        {
            StartCoroutine(ExitScene());
        }
        private IEnumerator ExitScene()
        {
            yield return new WaitForSeconds(10);

            currentScene.Scene = currentScene.PreviousScene;
            loadSceneEvent?.Raise(currentScene);
        }
    }
}

