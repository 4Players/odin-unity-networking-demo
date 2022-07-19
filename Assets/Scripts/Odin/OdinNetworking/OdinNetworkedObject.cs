using System.Collections;
using ElRaccoone.Tweens;
using Odin.OdinNetworking.Messages;
using UnityEngine;

namespace Odin.OdinNetworking
{
    /// <summary>
    /// The base class for networked objects, i.e. objects that are shared within the network with all peers. Add this
    /// component to prefabs that can be spawned by player and that should also be spawned for everyone else in the
    /// network. Every networked object has an owner that has authority over the object and an ObjectId that is unique
    /// for the owners objects. The combination of owner and objectid uniquely identifies every object in the network.
    ///
    /// Every prefab that can be spawned in the network needs to be entered in the
    /// <see cref="Odin.OdinNetworking.OdinNetworkManager.spawnablePrefabs"/> list. This way, the index of the object
    /// that should be spawned needs to be transferred over the network (just one byte) instead of the name which would
    /// take up many more bytes. 
    /// </summary>
    public class OdinNetworkedObject : OdinNetworkItem
    {
        /// <summary>
        /// The owner of this object. The owner is responsible to update the location and variables with the network.
        /// </summary>
        [HideInInspector]
        public OdinNetworkItem Owner;
        
        /// <summary>
        /// Every networked object has a unique ObjectId (for the owner). As the ObjectId is a byte, in total 256 objects
        /// can be handled or spawned by each owner. This value is set automatically when the object is spawned.
        /// </summary>
        [HideInInspector]
        public byte ObjectId;
        
        /// <summary>
        /// If this object has been spawned, a PrefabId is stored for the object. The PrefabId is the index of the prefab
        /// list stored in the <see cref="Odin.OdinNetworking.OdinNetworkManager.spawnablePrefabs"/> list.
        /// </summary>
        [HideInInspector]
        public byte PrefabId;

        /// <summary>
        /// A dirty flag that indicates if this object has been updated since the last send interval.
        /// </summary>
        [HideInInspector] 
        public bool IsUpdated = false;
    
        /// <summary>
        /// The lifetime of the object after being spawned. If 0, the object does not deprecate and therefore lives
        /// forever until destroyed manually. You should always set a value here to make sure that forgotten objects
        /// generate bandwidth. The lifetime is given in seconds. After timeout, the object will self-destroy over the
        /// network, thus it will be destroyed on all clients.
        /// </summary>
        [Tooltip("This defines the lifetime of this object in seconds. If zero, it needs to be destroyed manually.")]
        public float LifeTime = 0;
        
        /// <summary>
        /// Called in the first frame after a peers player object has been created. It initiates self-destruction if
        /// <see cref="LifeTime"/> is greater than 0.  
        /// </summary>
        /// <remarks>You should call the base class implementation if you override this function.</remarks>
        public override void OnStartClient()
        {
            base.OnStartClient();

            if (LifeTime > 0)
            {
                StartCoroutine(SelfDestroy(LifeTime));
            }
        }
    
        private IEnumerator SelfDestroy(float f)
        {
            yield return new WaitForSeconds(f);
            Owner.DestroyManagedNetworkedObject(this);
        }

        /// <summary>
        /// Called if <see cref="Odin.OdinNetworking.OdinNetworkManager"/> received an updated status for this object
        /// over the network. This function updates the location and the sync vars and interpolates over the owners
        /// <see cref="Odin.OdinNetworking.OdinNetworkItem.SendInterval"/> if tween is set to true. Otherwise the
        /// values are just set to the new values.
        /// </summary>
        /// <param name="managedObject">The value container for this object.</param>
        /// <param name="tween">If true the new values will be interpolated over some time, otherwise they will be set
        /// immediately.</param>
        public void OnUpdatedFromNetwork(OdinUserDataManagedObject managedObject, bool tween = true)
        {
            if (tween)
            {
                this.TweenLocalPosition(managedObject.Transform.Position, Owner.SendInterval);
                this.TweenLocalRotation(managedObject.Transform.Rotation.eulerAngles, Owner.SendInterval);
                this.TweenLocalScale(managedObject.Transform.Scale, Owner.SendInterval);    
            }
            else
            {
                transform.localPosition = managedObject.Transform.Position;
                transform.localRotation = managedObject.Transform.Rotation;
                transform.localScale = managedObject.Transform.Scale;
            }
        
            ReadSyncVars(managedObject.SyncVars);
        }
    }
}
