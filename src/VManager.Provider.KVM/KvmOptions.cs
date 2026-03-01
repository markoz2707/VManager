namespace VManager.Provider.KVM;

public class KvmOptions
{
    public string LibvirtUri { get; set; } = "qemu:///system";
    public string DefaultStoragePool { get; set; } = "default";
    public string DefaultDiskFormat { get; set; } = "qcow2";
    public string DefaultDiskPath { get; set; } = "/var/lib/libvirt/images";
}
