using System.Runtime.Serialization;

namespace HASS.Agent.Core.Enums
{
    public enum MediaPlayerState
    {
        [EnumMember(Value = "off")]
        Off,

        [EnumMember(Value = "idle")]
        Idle,

        [EnumMember(Value = "playing")]
        Playing,

        [EnumMember(Value = "paused")]
        Paused
    }
}
