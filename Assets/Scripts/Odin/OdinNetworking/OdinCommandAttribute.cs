using System;

namespace Odin.OdinNetworking
{
    /// <summary>
    /// Use this attribute in a member property of any subclass of <see cref="Odin.OdinNetworking.OdinNetworkItem"/> to
    /// mark a function as a command
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    public class OdinCommandAttribute: Attribute
    {
        
    }
}