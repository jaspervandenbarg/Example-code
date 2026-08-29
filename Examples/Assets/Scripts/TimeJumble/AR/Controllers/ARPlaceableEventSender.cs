using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Jigsar.Events;

namespace Jigsar.AR
{
    public class ARPlaceableEventSender : MonoBehaviour
    {
        [SerializeField] private VoidEvent ToggleARObjectEvent;
        public void SendEvent() => ToggleARObjectEvent?.Raise();
    }
}

