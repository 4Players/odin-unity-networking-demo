using System;
using Odin.OdinNetworking;

namespace OdinNetworking
{
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = true)]
    public class OdinSyncVar : Attribute
    {
        private string _hook;
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
