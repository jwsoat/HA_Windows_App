using HASS.Agent.Core.Enums;
using HASS.Agent.Core.Models.Internal;
using HASS.Agent.Shared.Enums;

namespace HASS.Agent.Core.HomeAssistant
{
    public interface IHassApiService
    {
        HassManagerStatus ManagerStatus { get; }
        string HaVersion { get; }

        List<string> AutomationList { get; }
        List<string> ScriptList { get; }
        List<string> InputBooleanList { get; }
        List<string> SceneList { get; }
        List<string> SwitchList { get; }
        List<string> LightList { get; }
        List<string> CoverList { get; }
        List<string> ClimateList { get; }
        List<string> MediaPlayerList { get; }

        Task<HassManagerStatus> InitializeAsync();
        Task<bool> ProcessQuickActionAsync(QuickAction action);
        Task FireEventAsync(string eventType, object? data = null);
        Task<bool> CheckConnectionAsync();
    }
}
