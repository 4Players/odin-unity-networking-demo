using System;
using System.Collections.Generic;
using System.Reflection;
using ElRaccoone.Tweens;
using OdinNative.Odin.Peer;
using OdinNetworking;
using UnityEngine;

namespace Odin.OdinNetworking
{
    public class OdinNetworkIdentity : MonoBehaviour
    {
        public Peer Peer;

        [Header("Network Settings")]
        [Tooltip("Sync the transform as part of the peers user data")]
        [SerializeField] private bool SyncTransform = true;
        
        [Tooltip("Sync the animator")]
        [SerializeField] private bool SyncAnimator = true;

        [Tooltip("The number of seconds until the next update is sent")]
        [SerializeField] private float SendInterval = 0.05f;
        
        private OdinNetworkWriter _lastUserData = null;
        private OdinNetworkWriter _lastNetworkedObjectUpdate = null;
        Dictionary<int, OdinNetworkWriter> _lastNetworkedObjectStates = new Dictionary<int, OdinNetworkWriter>();
        private float _lastSent;

        private Animator _animator;

        private List<OdinNetworkedObject> _spawnedObjects = new List<OdinNetworkedObject>();
        private int _objectId = 0;
        
        private class OdinSyncVarInfo
        {
            public FieldInfo FieldInfo;
            public OdinSyncVar OdinSyncVar;
            public object LastValue;
            
            public void OnSerialize(OdinNetworkWriter writer, object instance)
            {
                object currentValue = FieldInfo.GetValue(instance); 
            }
        }
        
        private Dictionary<string, OdinSyncVarInfo> _syncVars = new Dictionary<string, OdinSyncVarInfo>();

        public virtual void OnStartClient()
        {
            // Get Attributes
            foreach (var field in GetType().GetFields())
            {
                OdinSyncVar syncVar = (OdinSyncVar) Attribute.GetCustomAttribute(field, typeof(OdinSyncVar));
                if (syncVar != null)
                {
                    Debug.Log($"Found sync var: {field.Name} with hook: {syncVar.hook}");

                    _syncVars[field.Name] = new OdinSyncVarInfo { FieldInfo = field, OdinSyncVar = syncVar, LastValue = field.GetValue(this) };
                }
            }
            
            // Get Animator
            if (!_animator)
            {
                _animator = GetComponentInChildren<Animator>();    
            }
            
            // If this is not the local player, set rigid body to be kinetic (i.e. position and rotation is not part of
            // physics calculation
            if (!IsLocalPlayer())
            {
                foreach (var rb in GetComponentsInChildren<Rigidbody>())
                {
                    rb.isKinematic = true;
                }
            }
        }

        public virtual void OnStartLocalClient()
        {
        }

        public virtual void OnStopClient()
        {
            
        }

        public virtual void OnStopLocalClient()
        {
            
        }
        
        private void FixedUpdate()
        {
            if (!IsLocalPlayer())
            {
                return;
            }
            
            // Wait for the next slot for sending data
            if (Time.time - _lastSent > SendInterval)
            {
                // Sync networked objects
                if (_spawnedObjects.Count > 0)
                {
                    List<OdinNetworkWriter> newObjectStates = new List<OdinNetworkWriter>();
                    foreach (var networkedObject in _spawnedObjects)
                    {
                        OdinNetworkWriter writer = new OdinNetworkWriter();
                        writer.Write(networkedObject.NetworkId);
                        writer.Write(networkedObject.gameObject.transform);

                        if (!_lastNetworkedObjectStates.ContainsKey(networkedObject.NetworkId))
                        {
                            newObjectStates.Add(writer);
                            _lastNetworkedObjectStates[networkedObject.NetworkId] = writer;
                        }
                        else if (!writer.IsEqual(_lastNetworkedObjectStates[networkedObject.NetworkId]))
                        {
                            newObjectStates.Add(writer);
                            _lastNetworkedObjectStates[networkedObject.NetworkId] = writer;
                        }
                    }
                    
                    OdinMessage message = new OdinMessage(OdinMessageType.UpdateNetworkedObject);
                    message.Write(newObjectStates.Count);
                    if (newObjectStates.Count > 0)
                    {
                        foreach (var objectState in newObjectStates)
                        {
                            message.Write(objectState);
                        }

                        if (!message.IsEqual(_lastNetworkedObjectUpdate))
                        {
                            Debug.Log($"Sending Network Update Message with Bytes: {message.Cursor}");
                            OdinNetworkManager.Instance.SendMessage(message);
                            _lastNetworkedObjectUpdate = message;
                        }                        
                    }
                }
                
                // Compile user data
                OdinNetworkWriter userData = new OdinNetworkWriter();
                
                // Update Transform
                userData.Write(SyncTransform);
                if (SyncTransform)
                {
                    userData.Write(transform);
                }
                
                // Update Animator
                userData.Write(SyncAnimator && _animator);
                if (SyncAnimator && _animator)
                {
                    userData.Write(_animator.parameterCount);
                    foreach (var param in _animator.parameters)
                    {
                        if (param.type == AnimatorControllerParameterType.Bool)
                        {
                            userData.Write(OdinPrimitive.Bool);
                            userData.Write(_animator.GetBool(param.name));       
                        } 
                        else if (param.type == AnimatorControllerParameterType.Float)
                        {
                            userData.Write(OdinPrimitive.Float);
                            userData.Write(_animator.GetFloat(param.name));
                        }
                        else if (param.type == AnimatorControllerParameterType.Int)
                        {
                            userData.Write(OdinPrimitive.Integer);
                            userData.Write(_animator.GetInteger(param.name));
                        }
                    }
                }

                // Update Sync Vars
                byte numberOfDirtySyncVars = 0;
                Dictionary<string, object> dirtySyncVars = new Dictionary<string, object>();
                foreach (var key in _syncVars.Keys)
                {
                    OdinSyncVarInfo syncInfo = _syncVars[key];
                    object currentValue = syncInfo.FieldInfo.GetValue(this);
                    if (currentValue != syncInfo.LastValue)
                    {
                        Debug.Log($"Value for SyncVar {key} changed. Old value: {syncInfo.LastValue}, new Value: {currentValue}");
                        
                        dirtySyncVars[syncInfo.FieldInfo.Name] = currentValue;
                        syncInfo.LastValue = currentValue;
                        numberOfDirtySyncVars++;
                    }
                }

                userData.Write(numberOfDirtySyncVars);
                foreach (var key in dirtySyncVars.Keys)
                {
                    userData.Write(key);
                    userData.Write(dirtySyncVars[key]);
                }

                // Compare if things have changed, then send an update
                if (!userData.IsEqual(_lastUserData))
                {
                    Debug.Log($"Sending user data update: {userData.Cursor}");
                    OdinNetworkManager.Instance.SendUserDataUpdate(userData);
                }

                // Store last user data
                _lastUserData = userData;
                _lastSent = Time.time;
            }
        }

        public void MessageReceived(OdinNetworkIdentity sender, OdinNetworkReader reader)
        {
            OdinMessageType messageType = reader.ReadMessageType();

            if (messageType == OdinMessageType.UpdateSyncVar)
            {
                var syncVarName = reader.ReadString();
                var currentValue = reader.ReadObject();

                OdinSyncVarInfo syncInfo = _syncVars[syncVarName];
                syncInfo.FieldInfo.SetValue(this, currentValue);
            } 
            else if (messageType == OdinMessageType.SpawnPrefab)
            {
                var prefabName = reader.ReadString();
                var objectId = reader.ReadInt();
                var position = reader.ReadVector3();
                var rotation = reader.ReadQuaternion();
                OdinNetworkManager.Instance.SpawnPrefab(this, prefabName, objectId, position, rotation);
            } 
            else if (messageType == OdinMessageType.UpdateNetworkedObject)
            {
                int numberOfObjects = reader.ReadInt();
                for (int i = 0; i < numberOfObjects; i++)
                {
                    var networkId = reader.ReadInt();
                    var networkedObject = OdinNetworkManager.Instance.FindNetworkedObject(sender.Peer.Id, networkId);
                    if (networkedObject)
                    {
                        networkedObject.TweenLocalPosition(reader.ReadVector3(), SendInterval);
                        networkedObject.TweenLocalRotation(reader.ReadQuaternion().eulerAngles, SendInterval);
                        networkedObject.TweenLocalScale(reader.ReadVector3(), SendInterval);
                    }
                }
            }
        }

        public void UserDataUpdated(OdinNetworkReader reader)
        {
            // Read transform
            var hasTransform = reader.ReadBoolean();
            if (hasTransform)
            {
                gameObject.TweenLocalPosition(reader.ReadVector3(), SendInterval);
                gameObject.TweenLocalRotation(reader.ReadQuaternion().eulerAngles, SendInterval);
                gameObject.TweenLocalScale(reader.ReadVector3(), SendInterval);
            }
            
            // Read Animator
            var hasAnimator = reader.ReadBoolean();
            if (hasAnimator)
            {
                _animator = GetComponent<Animator>();
                var numberOfParams = reader.ReadInt();
                for (int i = 0; i < numberOfParams; i++)
                {
                    var param = _animator.GetParameter(i);
                    OdinPrimitive primitive = reader.ReadPrimitiveType();
                    if (primitive == OdinPrimitive.Bool)
                    {
                        _animator.SetBool(param.name, reader.ReadBoolean());
                    } 
                    else if (primitive == OdinPrimitive.Float)
                    {
                        _animator.SetFloat(param.name, reader.ReadFloat());
                    }
                    else if (primitive == OdinPrimitive.Integer)
                    {
                        _animator.SetInteger(param.name, reader.ReadInt());
                    }
                }
            }
            
            // Sync Vars
            var numberOfSyncVars = reader.ReadByte();
            if (numberOfSyncVars > 0)
            {
                for (byte i = 0; i < numberOfSyncVars; i++)
                {
                    var syncVarName = reader.ReadString();
                    var currentValue = reader.ReadObject();
                
                    OdinSyncVarInfo syncInfo = _syncVars[syncVarName];
                    if (syncInfo != null)
                    {
                        syncInfo.FieldInfo.SetValue(this, currentValue);    
                    }
                    else
                    {
                        Debug.LogError($"Could not find Syncvar with name {syncVarName}");
                    }                    
                }
            }
        }
        
        public bool IsLocalPlayer()
        {
            return OdinNetworkManager.Instance.LocalPlayer == this;
        }

        public void Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            Spawn(prefab.name, position, rotation);
        }

        public void Spawn(string prefabName, Vector3 position, Quaternion rotation)
        {
            var networkedObject = OdinNetworkManager.Instance.SpawnPrefab(this, prefabName, _objectId, position, rotation);
            if (networkedObject == null)
            {
                Debug.LogWarning($"Could not spawn prefab {prefabName}");
                return;
            }
            
            _spawnedObjects.Add(networkedObject);

            OdinMessage message = new OdinMessage(OdinMessageType.SpawnPrefab);
            message.Write(prefabName);
            message.Write(_objectId);
            message.Write(position);
            message.Write(rotation);
            OdinNetworkManager.Instance.SendMessage(message, false);

            _objectId++;
        }
    }
}
