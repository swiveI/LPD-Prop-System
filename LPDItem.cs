using UdonSharp;
using UnityEngine;

namespace LocalPoliceDepartment.Props
{
    /// <summary>
    /// Base class for items that are spawned through the LPD prop system
    /// </summary>
    public class LPDItem : UdonSharpBehaviour
    {
        [Tooltip("If True then only one of this item can be spawned for each player.")]
        public bool Exclusive;
        
        protected LPDProp prop;
        protected bool initialized = false;
    
        public virtual void InitializeItem(LPDProp p) { prop = p; initialized = true; }
        public virtual void OnCleanup() { }
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