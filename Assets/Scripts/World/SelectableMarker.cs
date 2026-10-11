using UnityEngine;

namespace RtsWarcraft
{
    // -----------------------------------------------------------------------
    //  Attached to anything the mouse can pick: units, buildings, gold mines
    //  and trees.  SelectionManager raycasts for this component.
    // -----------------------------------------------------------------------
    public class SelectableMarker : MonoBehaviour
    {
        public UnitController unit;
        public BuildingController building;
        public ResourceNode node;

        public void Init(UnitController u) { unit = u; building = null; node = null; }
        public void Init(BuildingController b) { unit = null; building = b; node = null; }
        public void Init(ResourceNode n) { unit = null; building = null; node = n; }

        public bool IsOwn(RTSPlayer p)
        {
            if (unit != null) return unit.Owner == p;
            if (building != null) return building.Owner == p;
            return false;
        }
    }
}
