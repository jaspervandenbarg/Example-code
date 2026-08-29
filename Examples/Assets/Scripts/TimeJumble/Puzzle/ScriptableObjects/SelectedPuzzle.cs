using UnityEngine;
using Sirenix.OdinInspector;

namespace Jigsar.Puzzles
{
    [CreateAssetMenu(fileName = "SelectedPuzzle", menuName = "Jigsar/ScriptableObjects/Locations/SelectedPuzzle")]
    public class SelectedPuzzle : ScriptableObject
    {
        [SerializeField, ReadOnly] private PuzzleLocation puzzle;
        [SerializeField, ReadOnly] private PuzzleLocation previousPuzzle;
        public PuzzleLocation Puzzle { get => puzzle; set => SetPuzzle(value); }
        public PuzzleLocation PreviousPuzzle => previousPuzzle;

        private void SetPuzzle(PuzzleLocation value)
        {
            previousPuzzle = puzzle;
            puzzle = value;
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                puzzle = null;
                previousPuzzle = null;
            }
#endif
        }
    }
}

