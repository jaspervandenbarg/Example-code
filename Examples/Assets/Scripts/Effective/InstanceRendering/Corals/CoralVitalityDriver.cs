using MARIS.ScenarioSimulation.SepeModel;
using System.Collections.Generic;
using UnityEngine;

namespace MARIS.Rendering.InstanceRendering.Corals
{
    /// <summary>
    /// Drives coral vitality from the SEPE model. Put this on the same GameObject as a
    /// <see cref="CoralInstanceManager"/>. Editor event listeners call the Update methods when the
    /// user changes temperature, duration or management in the UI. Each call runs the model and
    /// forwards the per-species vitality to the manager, which reacts per group (disappear or bleach).
    /// </summary>
    [RequireComponent(typeof(CoralInstanceManager))]
    public class CoralVitalityDriver : MonoBehaviour
    {
        [SerializeField] private CoralInstanceManager coralManager;

        [Tooltip("Sea temperature in degrees Celsius.")]
        [SerializeField] private float temperature = 20f;

        [Tooltip("Marine heat-wave duration in days.")]
        [SerializeField] private float duration = 1f;

        [Tooltip("True when management measures are active.")]
        [SerializeField] private bool management = false;

        private void Awake()
        {
            if (coralManager == null)
                coralManager = GetComponent<CoralInstanceManager>();
        }

        private void OnEnable()
        {
            RunModel();
        }

        public void UpdateTemperature(float value)
        {
            temperature = value;
            RunModel();
        }

        public void UpdateDuration(float value)
        {
            duration = value;
            RunModel();
        }

        public void UpdateManagement(bool value)
        {
            management = value;
            RunModel();
        }

        /// <summary>Runs the SEPE model and pushes each species vitality into the coral manager.</summary>
        public void RunModel()
        {
            if (coralManager == null)
                return;

            Dictionary<string, float> model = SepeModel.RunModel(temperature, duration, management);

            foreach (KeyValuePair<string, float> kv in model)
                coralManager.SetSpeciesVitality(kv.Key, kv.Value);
        }
    }
}
