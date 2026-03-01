using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace HyperV.Agent.Controllers;

/// <summary>
/// Conditionally removes HyperV-specific controllers when running on KVM.
/// </summary>
public class HypervisorControllerFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
{
    private readonly bool _isKvm;

    public HypervisorControllerFeatureProvider(bool isKvm)
    {
        _isKvm = isKvm;
    }

    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        if (_isKvm)
        {
            // Remove HyperV-specific controllers when running on KVM
            var controllersToRemove = feature.Controllers
                .Where(t => t.Name == "HyperVFeaturesController"
                         || t.Name == "ReplicationController"
                         || t.Name == "StorageQoSController"
                         || t.Name == "ImageManagementController")
                .ToList();

            foreach (var controller in controllersToRemove)
            {
                feature.Controllers.Remove(controller);
            }
        }
    }
}
