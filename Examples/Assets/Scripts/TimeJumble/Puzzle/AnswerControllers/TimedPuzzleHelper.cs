using Jigsar.Events;
using Jigsar.Location;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using Jigsar.Audio;

namespace Jigsar.Puzzles
{
    public class TimedPuzzleHelper : MonoBehaviour
    {
        [Tooltip("After how much time spend on the puzzle and not having used a hint should the players get reminded they can use hints.\n Time in seconds")]
        [SerializeField] private int remindTime;
        [SerializeField] private CurrentLocation currentLocation;
        [SerializeField] private LocalizedStringEvent chatboxEvent;
        [SerializeField] private LocalizedString chatboxString;
        [SerializeField] private AudioElement audioElement;
        // Start is called before the first frame update
        void Start()
        {
            StartCoroutine(TimeTillReminding(remindTime));
        }

        private IEnumerator TimeTillReminding(int time)
        {
            //Debug.Log(currentLocation.Puzzle.TimeSpent);
            //int timeToWait = time > currentLocation.Puzzle.TimeSpent ? time - currentLocation.Puzzle.TimeSpent : time;

            //still works when object is disabled

            yield return new WaitForSecondsRealtime(time);

            if (!gameObject.activeSelf) yield break;

            if (currentLocation?.Puzzle?.Progress?.hintsTaken == 0)
            {
                chatboxEvent?.Raise(chatboxString);
                UIAudioManager.Instance?.PlayOnSource(audioElement.Clip, audioElement.Volume, audioElement.Pitch);
            }

        }
    }
}

