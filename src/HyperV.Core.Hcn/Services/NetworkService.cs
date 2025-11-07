using System;
using System.ComponentModel;
using HyperV.Core.Hcn.Interop;

namespace HyperV.Core.Hcn.Services;

/// <summary>Serwis tworzenia prostych sieci NAT (HCN).</summary>
public class NetworkService
{
    /// <summary>Tworzy sieć NAT o podanej nazwie i prefiksie.</summary>
    public virtual Guid CreateNATNetwork(string name, string addressPrefix = "192.168.100.0/24")
    {
        var id = Guid.NewGuid();
        var json = $@"{{
          ""Name"": ""{name}"",
          ""Type"": ""NAT"",
          ""Ipam"": {{ ""Type"": ""Static"", ""Subnets"": [ {{ ""IpAddressPrefix"": ""{addressPrefix}"" }} ] }}
        }}";
        var hr = HcnNative.HcnCreateNetwork(id, json, out var handle, out var err);
        if (hr != 0) throw new Win32Exception(hr);
        try { return id; }
        finally { if (handle != IntPtr.Zero) HcnNative.HcnCloseNetwork(handle); }
    }

    public virtual void DeleteNetwork(Guid id)
    {
        var hr = HcnNative.HcnDeleteNetwork(id, out var err);
        if (hr != 0) throw new Win32Exception(hr);
    }

    public virtual IntPtr OpenNetwork(Guid id)
    {
        var hr = HcnNative.HcnOpenNetwork(id, out var handle, out var err);
        if (hr != 0) throw new Win32Exception(hr);
        return handle;
    }

    public virtual Guid CreateEndpoint(Guid networkId, string name, string ipAddress = "")
    {
        var endpointId = Guid.NewGuid();
        var json = string.IsNullOrEmpty(ipAddress) 
            ? $@"{{""Name"": ""{name}""}}"
            : $@"{{""Name"": ""{name}"", ""IpAddress"": ""{ipAddress}""}}";
        
        var hr = HcnNative.HcnCreateEndpoint(networkId, endpointId, json, out var handle, out var err);
        if (hr != 0) throw new Win32Exception(hr);
        try { return endpointId; }
        finally { if (handle != IntPtr.Zero) HcnNative.HcnCloseEndpoint(handle); }
    }

    public virtual void DeleteEndpoint(Guid endpointId)
    {
        var hr = HcnNative.HcnDeleteEndpoint(endpointId, out var err);
        if (hr != 0) throw new Win32Exception(hr);
    }

    public virtual string QueryEndpointProperties(Guid endpointId, string query = "")
    {
        var hr = HcnNative.HcnOpenEndpoint(endpointId, out var handle, out var err);
        if (hr != 0) throw new Win32Exception(hr);
        
        try
        {
            hr = HcnNative.HcnQueryEndpointProperties(handle, query, out var result, out err);
            if (hr != 0) throw new Win32Exception(hr);
            return result ?? "{}";
        }
        finally
        {
            if (handle != IntPtr.Zero) HcnNative.HcnCloseEndpoint(handle);
        }
    }

    public virtual string QueryNetworkProperties(Guid networkId, string query = "")
    {
        var handle = OpenNetwork(networkId);
        try
        {
            var hr = HcnNative.HcnQueryNetworkProperties(handle, query, out var result, out var err);
            if (hr != 0) throw new Win32Exception(hr);
            return result ?? "{}";
        }
        finally
        {
            if (handle != IntPtr.Zero) HcnNative.HcnCloseNetwork(handle);
        }
    }

    /// <summary>Listuje wszystkie sieci HCN.</summary>
    public virtual string ListNetworks()
    {
        
        var hr = HcnNative.HcnEnumerateNetworks("", out var result, out var err);
        if (hr != 0) throw new Win32Exception(hr);
        return result ?? "[]";
    }

    /// <summary>Listuje wszystkie endpointy HCN.</summary>
    public virtual string ListEndpoints()
    {
        var hr = HcnNative.HcnEnumerateEndpoints("", out var result, out var err);
        if (hr != 0) throw new Win32Exception(hr);
        return result ?? "[]";
    }
}
