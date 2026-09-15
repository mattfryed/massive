using UnityEngine;
using UnityEngine.EventSystems;

namespace Massive.AttractStudy
{
    // Native EventSystem selection keeps all Rewired players on one shared menu.
    public sealed class AttractModeOption : MonoBehaviour,ISelectHandler
    {
        [System.NonSerialized] public AttractModeSelector owner;
        [System.NonSerialized] public int index;
        public void OnSelect(BaseEventData data){if(owner!=null)owner.SelectOption(index);}
    }
}
