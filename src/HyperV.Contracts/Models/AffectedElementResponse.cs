using System.Collections.Generic;

namespace HyperV.Contracts.Models
{
    public class AffectedElementResponse
    {
        public string ElementId { get; set; }
        public string ElementName { get; set; }
        public string ElementType { get; set; }
        public List<ElementEffect> Effects { get; set; } = new List<ElementEffect>();
    }

    public class ElementEffect
    {
        public ElementEffectType Type { get; set; }
        public string Description { get; set; }
    }

    public enum ElementEffectType
    {
        Unknown = 0,
        Other = 1,
        ExclusiveUse = 2,
        PerformanceImpact = 3,
        ElementIntegrity = 4
    }
}