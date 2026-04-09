using UdonSharp;
using UnityEngine;
using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace LocalPoliceDepartment.Props
{
    [RequireComponent(typeof(VRC_Pickup))]
    public class LPDProp : UdonSharpBehaviour
    {
        public VRC_Pickup PickupComp;
        [SerializeField] private PropsManager propsManager;
        [SerializeField] private BoxCollider boxCollider;
        [SerializeField] private SphereCollider sphereCollider;
        [SerializeField] int decayTimer = 180;
        private Rigidbody _rigidbody;
        
        /// <summary>
        /// Generic data field to be used by items to sync data.
        /// Use this to set the state of an item and sync it.
        /// </summary>
        [UdonSynced] public string ItemSerializedData = "";
        private string oldItemData = "";
        
        /// <summary>
        /// ID number to identify the item that this prop is currently representing. -1 means no item.
        /// The item id corresponds to the index of the item in the PropsManager items array.
        /// </summary>
        [UdonSynced] public int ItemId = -1;
        private int currentItemId = -1;

        //used for custom image/video/string loading
        [UdonSynced] private VRCUrl syncedUrl;
        
        /// <summary>
        /// Returns the VRCUrl that is currently synced for this prop.
        /// Get the url from here for your LPDItem script.
        /// </summary>
        /// <returns></returns>
        public VRCUrl GetSyncedUrl() { return syncedUrl; }

        /// <summary>
        /// VRCUrl field for syncing urls for items that need it as they cant be generated from strings at runtime.
        /// Setting this does not automatically sync it. You must call RequestSerialization or UpdateItemData after setting it to sync it.
        /// </summary>
        /// <param name="url">The VRCUrl to sync</param>
        public void SetSyncedUrl(VRCUrl url)
        {
            if (Networking.LocalPlayer != PropOwner)
            {
                Debug.LogError($"{Networking.LocalPlayer.displayName} attempted to set url on prop they do not own!");
                return;
            }
            syncedUrl = url;
        }
        
        /// <summary>
        /// The owner of this prop.
        /// Props should be playerobjects so the owner will not change.
        /// </summary>
        public VRCPlayerApi PropOwner { get; private set; }
        private LPDItem _itemBehaviour;

        private void Start()
        {
            PropOwner = Networking.GetOwner(gameObject);
            if (PropOwner != Networking.LocalPlayer) PickupComp.pickupable = false;
            
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.isKinematic = true;
            _rigidbody.useGravity = false;
            
            //tmp fix becuase vrchat shipped broken persistence
            SendCustomEventDelayedSeconds(nameof(OnDeserialization), 5);
        }

        /// <summary>
        /// For props that need to specify a LPDItem script to handle its behaviour, set the item behaviour with this function after spawning the item.
        /// By default the prop searches for an LPDItem script on the spawned item and uses that as its behaviour, but if you need to specify a specific script or the script is not on the root of the spawned item you can use this function to set it.
        /// </summary>
        /// <param name="behaviour"></param>
        public void SetEventListener(LPDItem behaviour)
        {
            _itemBehaviour = behaviour;
        }

        /// <summary>
        /// Returns the LPDItem script that is currently set as the behaviour for this prop. This is the script that will receive events when the item is used, picked up, dropped, or collides with something, as well as receive network updates.
        /// </summary>
        /// <returns></returns>
        public LPDItem GetItemBehaviour()
        {
            return _itemBehaviour;
        }

        /// <summary>
        /// Despawns the current item on this prop and resets it to be empty.
        /// </summary>
        public void ReleaseProp()
        {
            ItemId = -1;
            RequestSerialization();
            OnDeserialization();
        }

        /// <summary>
        /// Sets the data for the current item and syncs it. This should be used by the owner of the prop to update the state of the item and sync it to other players. Non owners should use RequestItemUpdate to ask the owner to update the item data for them.
        /// </summary>
        /// <param name="data">Data to be synced. You will need to come up with your own format that fits your specific LPDItem</param>
        public void UpdateItemData(string data)
        {
            ItemSerializedData = data;
            RequestSerialization();
            OnDeserialization();
        }

        /// <summary>
        /// Calls an event on the LPDItem behaviour script across the network.
        /// </summary>
        /// <param name="eventName">Name of the event to call</param>
        /// <param name="target">The target of the event</param>
        public void SendNetworkEvent(string eventName, NetworkEventTarget target)
        {
            SendCustomNetworkEvent(target, nameof(CallNetworkEvent), eventName);
        }
        
        /// <summary>
        /// Helper function to call network events on the item behaviour script. This is called by SendNetworkEvent and should not be called directly.
        /// </summary>
        /// <param name="eventName"></param>
        [NetworkCallable]
        public void CallNetworkEvent(string eventName)
        {
            if (_itemBehaviour != null)
            {
                _itemBehaviour.SendCustomEvent(eventName);
            }
        }
        

        /// <summary>
        /// Handles updating the state of the prop and any item it has spawned.
        /// Automatically called by VRChat's networking on remote clients.
        /// </summary>
        public override void OnDeserialization()
        {
            if (ItemId != currentItemId)
            {
                //item changed, destroy old object
                PickupComp.Drop();
                if (_itemBehaviour != null) _itemBehaviour.OnCleanup();
                
                _itemBehaviour = null;
                foreach (Transform child in transform) Destroy(child.gameObject);
                currentItemId = ItemId;
                
                //item changed to nothing
                if (ItemId == -1)
                {
                    Debug.Log("prop freed");
                    ItemSerializedData = "";
                    _rigidbody.isKinematic = true;
                    _rigidbody.useGravity = false;
                    transform.position = Vector3.down;
                    transform.rotation = Quaternion.identity;
                    return;
                }
                
                if (!PropOwner.isLocal) PickupComp.pickupable = false;
                
                //spawn new item
                GameObject item = Instantiate(propsManager.items[ItemId], transform);
                item.transform.localPosition = Vector3.zero;
                item.transform.localRotation = Quaternion.identity;
                item.SetActive(true);
                
                PickupComp.InteractionText = $"{propsManager.items[ItemId].name} (<color=#c91853>{PropOwner.displayName}</color>)";
                
                boxCollider.size = new Vector3(.1f, .1f, .1f);
                sphereCollider.radius = .05f;
                MeshFilter meshFilter = item.GetComponent<MeshFilter>();
                if (meshFilter == null) meshFilter = item.GetComponentInChildren<MeshFilter>();
                if (meshFilter != null)
                {
                    Vector3 size = meshFilter.sharedMesh.bounds.size;
                    size.x *= item.transform.localScale.x;
                    size.y *= item.transform.localScale.y;
                    size.z *= item.transform.localScale.z;
                    boxCollider.size = size;
                    sphereCollider.radius = Mathf.Max(size.x, size.y, size.z) / 2f;
                }
                else
                {
                    //try skinnedmesh
                    SkinnedMeshRenderer skinnedMeshRenderer = item.GetComponent<SkinnedMeshRenderer>();
                    if (skinnedMeshRenderer != null)
                    {
                        Vector3 size = skinnedMeshRenderer.sharedMesh.bounds.size;
                        size.x *= item.transform.localScale.x;
                        size.y *= item.transform.localScale.y;
                        size.z *= item.transform.localScale.z;
                        boxCollider.size = size;
                        sphereCollider.radius = Mathf.Max(size.x, size.y, size.z) / 2f;
                    }
                }

                if (_itemBehaviour == null) _itemBehaviour = item.GetComponent<LPDItem>();
                if (_itemBehaviour != null)
                {
                    Debug.Log($"Prop {item.name} initialized with data: {ItemSerializedData}");
                    _itemBehaviour.InitializeItem(this);
                }
                return;
            }
            
            if (oldItemData == ItemSerializedData) return;
            oldItemData = ItemSerializedData;

            if (_itemBehaviour != null)
            {
                Debug.Log($"Prop updated with new data: {ItemSerializedData}");
                _itemBehaviour.OnItemNetworkUpdate(ItemSerializedData);
            }
        }

        /// <summary>
        /// To be used by non owners to ask an item to update its data.
        /// The Items logic may reject the update request.
        /// </summary>
        /// <param name="data">Data to update the item with</param>
        [NetworkCallable]
        public void RequestItemUpdate(string data)
        {
            if (_itemBehaviour == null) return;
            _itemBehaviour.OnItemUpdateRequested(data);
        }
        
        
        #region Passthrough Events
        public override void OnPickupUseDown()
        {
            if (_itemBehaviour != null) _itemBehaviour.OnItemUsed();
        }
        
        public override void OnPickupUseUp()
        {
            if (_itemBehaviour != null) _itemBehaviour.OnItemStopUse();
        }

        public override void OnPickup()
        {
            if (_itemBehaviour != null) _itemBehaviour.OnItemPickedUp();
        }

        public override void OnDrop()
        {
            if (_itemBehaviour != null) _itemBehaviour.OnItemDropped();
        }

        private void OnCollisionEnter(Collision other)
        {
            if (_itemBehaviour != null) _itemBehaviour.OnCollideEnter(other);
        }

        private void OnCollisionExit(Collision other)
        {
            if (_itemBehaviour != null) _itemBehaviour.OnCollideExit(other);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_itemBehaviour != null) _itemBehaviour.OnEnterTrigger(other);
        }
        
        private void OnTriggerExit(Collider other)
        {
            if (_itemBehaviour != null) _itemBehaviour.OnExitTrigger(other);
        }

        private void OnDestroy()
        {
            if (_itemBehaviour != null) _itemBehaviour.OnCleanup();
        }
        #endregion

#if UNITY_EDITOR && !COMPILER_UDONSHARP
        public void OnValidate()
        {
            if (propsManager == null)
            {
                propsManager = FindObjectOfType<PropsManager>();
            }
            if (PickupComp == null)
            {
                PickupComp = GetComponent<VRC_Pickup>();
            }
        }
#endif
    }
}