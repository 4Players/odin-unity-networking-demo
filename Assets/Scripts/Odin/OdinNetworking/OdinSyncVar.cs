using System;

namespace Odin.OdinNetworking
{
    /// <summary>
    /// Use this attribute in a member property of any subclass of <see cref="Odin.OdinNetworking.OdinNetworkItem"/> to
    /// mark this variable as a networked variable that will be synced with all clients across the network.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = true)]
    public class OdinSyncVar : Attribute
    {
        private string _hook;
        
        /// <summary>
        /// The name of the hook function that should be called whenever the value changed
        /// </summary>
        public string hook
        {
            get => this._hook;
            set => this._hook = value;
        }
        
        public OdinSyncVar(string hook = null)
        {
            this.hook = hook;
        }
    }
}
