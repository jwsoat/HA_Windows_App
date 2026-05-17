using HASS.Agent.Core.Models.Config;
using HASS.Agent.Core.Models.Internal;
using HASS.Agent.Shared.Models.HomeAssistant;

namespace HASS.Agent.Core.Configuration
{
    public interface ISettingsService
    {
        Task<bool> LoadAsync();
        Task<bool> LoadEntitiesAsync();
        bool StoreAppSettings();
        bool StoreQuickActions();
        bool StoreCommands();
        bool StoreSensors();
        bool Store();

        bool GetExtendedLoggingSetting();
        void SetExtendedLoggingSetting(bool enabled);
    }
}
