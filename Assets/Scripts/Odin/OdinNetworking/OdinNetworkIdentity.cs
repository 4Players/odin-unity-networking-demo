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

        [Tooltip("The number of seconds until the next update is sent")]
        [SerializeField] private float SendInterval = 0.05f;
        
        private OdinNetworkWriter _lastUserData = null;
        private float _lastSent;
        
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
            
            // Compile user data
            if (Time.time - _lastSent > SendInterval)
            {
                OdinNetworkWriter userData = new OdinNetworkWriter();
                userData.Write(SyncTransform);
                if (SyncTransform)
                {
                    userData.Write(transform.localPosition);
                    userData.Write(transform.localRotation);
                    userData.Write(transform.localScale);
                }

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

        public void MessageReceived(OdinNetworkReader reader)
        {
            OdinMessageType messageType = (OdinMessageType)reader.ReadByte();

            if (messageType == OdinMessageType.UpdateSyncVar)
            {
                var syncVarName = reader.ReadString();
                var currentValue = reader.ReadObject();

                OdinSyncVarInfo syncInfo = _syncVars[syncVarName];
                syncInfo.FieldInfo.SetValue(this, currentValue);
            }
        }

        public void UserDataUpdated(OdinNetworkReader reader)
        {
            var hasTransform = reader.ReadBoolean();
            if (hasTransform)
            {
                gameObject.TweenLocalPosition(reader.ReadVector3(), SendInterval);
                gameObject.TweenLocalRotation(reader.ReadQuaternion().eulerAngles, SendInterval);
                gameObject.TweenLocalScale(reader.ReadVector3(), SendInterval);
            }

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
    }
}
