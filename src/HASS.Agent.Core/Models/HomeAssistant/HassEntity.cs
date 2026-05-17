using HASS.Agent.Shared.Enums;

namespace HASS.Agent.Core.Models.HomeAssistant
{
    public class HassEntity
    {
        public HassEntity() { }

        public HassEntity(HassDomain domain, string entity)
        {
            Domain = domain;
            Entity = entity;
        }

        public HassDomain Domain { get; set; }
        public string Entity { get; set; } = string.Empty;
    }
}
