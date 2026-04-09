using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace LocalPoliceDepartment.Props
{
    /// <summary>
    /// Base class for items that are spawned through the LPD prop system
    /// </summary>
    public class LPDItem : UdonSharpBehaviour
    {
        [Tooltip("If True then only one of this item can be spawned for each player.")]
        public bool Exclusive;
        
        [Header("Pickup Settings")]
        [SerializeField] private Transform heldPosition;
        [SerializeField] private VRC_Pickup.PickupOrientation pickupOrientation = VRC_Pickup.PickupOrientation.Any;
        
        protected LPDProp prop;
        protected bool initialized = false;
    
        public virtual void InitializeItem(LPDProp p)
        {
            prop = p;
            if (heldPosition != null)
            {
                prop.PickupComp.orientation = pickupOrientation;
                prop.PickupComp.ExactGun = heldPosition;
                prop.PickupComp.ExactGrip = heldPosition;
            }
        }

        public virtual void OnCleanup()
        {
            prop.PickupComp.orientation = VRC_Pickup.PickupOrientation.Any;
            prop.PickupComp.ExactGun = null;
            prop.PickupComp.ExactGrip = null;
        }
        public virtual void OnItemNetworkUpdate(string args) { }
        public virtual void OnItemUpdateRequested(string args) { }
        public virtual void OnItemUsed() { }
        public virtual void OnItemStopUse() { }
        public virtual void OnItemDropped() { }
        public virtual void OnItemPickedUp() { }
        public virtual void OnCollideEnter(Collision other) { }
        public virtual void OnCollideExit(Collision other) { }
        public virtual void OnEnterTrigger(Collider other) { }
        public virtual void OnExitTrigger(Collider other) { }
    }
}