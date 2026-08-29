using UnityEngine;
using Jigsar.Events;
using Jigsar.Location;
using UnityEngine.Localization;
using System.Collections;

namespace Jigsar.Puzzles
{
    public class BasePuzzleObject : MonoBehaviour
    {
        [SerializeField] private CurrentLocation currentLocation;
        [SerializeField] private VoidEvent reloadUIEvent;

        // Start is called before the first frame update
        [SerializeField] private bool hasUIInput;
        public bool HasUIInput => hasUIInput;

        protected virtual void Awake()
        {
            //nothing yet
        }

        protected virtual void Start()
        {
            TogglePanelEvent();
        }

        protected virtual void TogglePanelEvent()
        {
            reloadUIEvent?.Raise();
            //toggleMainUI?.Raise(true);
        }

        private void OnEnable()
        {
            //Debug.Log((int)DateTime.Now.TimeOfDay.TotalSeconds);
            currentLocation.Puzzle.SetStartTime();
        }
        private void OnDisable()
        {
            //why is this null???
            if (currentLocation == null)
                return;
            if (!currentLocation.Puzzle.Completed)
                currentLocation?.Puzzle.UpdateTimeSpend();
            //Debug.Log(puzzleScritpableObject.TimeSpend);
        }
    }
}

