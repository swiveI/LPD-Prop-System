using LocalPoliceDepartment.Utilities;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace LocalPoliceDepartment.Props
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PropsManager : UdonSharpBehaviour
    {
        [Header("Items that can be spawned.\nThe items position in this array is its ItemID number.")]
        public GameObject[] items;

        [SerializeField, Tooltip("If checked then props will despawn when this game object is disabled or the PropsEnabled field is set to false.")]
        private bool despawnPropsOnDisable;

        private LPDProp[] props;
        int forceIndex = 0;

        private bool propsEnabled = true;
        /// <summary>
        /// If false then no more props will be allowed to spawn.
        /// If despawnPropsOnDisable is true then all currently spawned props will also be despawned when this is set to false.
        /// </summary>
        public bool PropsEnabled
        {
            get{ return propsEnabled;}

            set
            {
                if (!value && despawnPropsOnDisable) FreeAllProps();
                propsEnabled = value;
            }
        }


        /// <summary>
        /// Returns the local players props.
        /// </summary>
        public LPDProp[] GetProps
        {
            get{ return props;}
            private set { props = value; }
        }
        
        private void Start()
        {
            props = LPDUdonUtils.GetPlayerObjectsOfType<LPDProp>(Networking.LocalPlayer);
            foreach (var prop in props)
            {
                prop.transform.position = Vector3.zero;
                prop.transform.rotation = Quaternion.identity;
            }
        }

        private void OnDisable()
        {
            if (despawnPropsOnDisable) FreeAllProps();
        }

        private LPDProp GetAvailableProp(bool force = false)
        {
            foreach (LPDProp prop in props)
            {
                if (prop.ItemId == -1)
                {
                    return prop;
                }
            }
            
            if (force)
            {
                forceIndex++;
                if (forceIndex >= props.Length) forceIndex = 0;
                props[forceIndex].ReleaseProp();
                return props[forceIndex];
            }

            return null;
        }
        
        /// <summary>
        /// Spawns multiple items at once in front of the player.
        /// </summary>
        /// <param name="items">The IDs of the items to spawn</param>
        /// <param name="data">Data to initialize items with</param>
        /// <param name="force">Forces the item to spawn by despawning another item if there are no other available props</param>
        public void BulkSpawn(int[] items, string[] data, bool force = false)
        {
            // free all items and spawn new ones
            //FreeAllProps();
            
            Vector3 pos;
            Quaternion rot;
            DefaultPosition(out pos, out rot);
            for (int i = 0; i < items.Length; i++)
            {
                SpawnItem(items[i], pos, rot, data[i], force);
            }
        }
        
        /// <summary>
        /// Spawns multiple items at once at the provided positions and rotations.
        /// </summary>
        /// <param name="items">The IDs of the items to spawn</param>
        /// <param name="positions">The positions to spawn the items at</param>
        /// <param name="rotations">The rotation to spawn the items with</param>
        /// <param name="data">Data to initialize items with</param>
        /// <param name="force">Forces the item to spawn by despawning another item if there are no other available props</param>
        public void BulkSpawn(int[] items, Vector3[] positions, Quaternion[] rotations, string[] data, bool force = false)
        {
            // free all items and spawn new ones
            //FreeAllProps();
            
            for (int i = 0; i < items.Length; i++)
            {
                SpawnItem(items[i], positions[i], rotations[i], data[i], force);
            }
        }

        /// <summary>
        /// Spawns an item in front of the player.
        /// returns a refrence to the LPDProp component of the spawned item or null if the item failed to spawn.
        /// </summary>
        /// <param name="itemId">The ID of the item to spawn</param>
        /// <param name="initialData">Data to initialize item with</param>
        /// <param name="force">Forces the item to spawn by despawning another item if there are no other available props</param>
        /// <returns></returns>
        public LPDProp SpawnItem(int itemId, string initialData = "", bool force = false)
        {
            Vector3 position;
            Quaternion rotation;
            DefaultPosition(out position, out rotation);
            return SpawnItem(itemId, position, rotation, initialData, force);
        }
        
        /// <summary>
        /// Spawns an item at the provided position and rotation.
        /// returns a refrence to the LPDProp component of the spawned item or null if the item failed to spawn.
        /// </summary>
        /// <param name="itemId">The ID of the item to spawn</param>
        /// <param name="position">The positions to spawn the item at</param>
        /// <param name="rotation">The rotation to spawn the item with</param>
        /// <param name="initialData">Data to initialize item with</param>
        /// <param name="force">Forces the item to spawn by despawning another item if there are no other available props</param>
        /// <returns></returns>
        public LPDProp SpawnItem(int itemId, Vector3 position, Quaternion rotation, string initialData = "", bool force = false)
        {
            if (!PropsEnabled) return null;
            
            if (itemId < 0 || itemId >= items.Length)
            {
                Debug.LogError("InventorySystem: Tried to spawn item but Item id was out of range");
                return null;
            }
            
            //check if the item is exclusive
            LPDItem itemComp = items[itemId].GetComponent<LPDItem>();
            if (itemComp != null && itemComp.Exclusive)
            {
                //check if we already have one of these items spawned
                foreach (LPDProp userProps in props)
                {
                    if (userProps.ItemId == itemId)
                    {
                        Debug.LogWarning("InventorySystem: Tried to spawn exclusive item but you already have one spawned");
                        return null;
                    }
                }
            }
            
            LPDProp prop = GetAvailableProp(force);
            if (prop == null)
            {
                Debug.LogError("InventorySystem: Tried to spawn item but no props were available");
                return null;
            }

            prop.transform.position = position;
            prop.transform.rotation = rotation;
            
            prop.ItemId = itemId;
            prop.ItemSerializedData = initialData;
            prop.RequestSerialization();
            prop.OnDeserialization();
            return prop;
        }
        
        private void DefaultPosition(out Vector3 position, out Quaternion rotation)
        {
            VRCPlayerApi.TrackingData headTackingData = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            Vector3 forward = headTackingData.rotation * Vector3.forward;
            forward.y = 0;
            forward = forward.normalized;
            position = headTackingData.position + forward / 2f;
            rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        /// <summary>
        /// Removes all of the localplayers items.
        /// As the props are synced, other players will also see this players items disappear.
        /// </summary>
        public void FreeAllProps() //this will cleanup all props in the scene
        {
            foreach (LPDProp prop in props) prop.ReleaseProp();
        }
        
        /// <summary>
        /// Removes the item from the provided prop and despawns it.
        /// </summary>
        /// <param name="prop"></param>
        public void ReleaseProp(LPDProp prop)
        {
            prop.ItemId = 0;
            prop.gameObject.SetActive(false);
        }
    }
}