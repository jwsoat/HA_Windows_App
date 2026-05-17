using HASS.Agent.Core.Mqtt;
using HASS.Agent.Core.Services;
using Serilog;

namespace HASS.Agent.Core.Sensors;

/// <summary>
/// Background loop that drives sensor publishing: every ~750ms it checks every sensor's update
/// interval, calls PublishStateAsync when due, and on the first publish after every (re)connect
/// pushes a fresh availability message. Autodiscovery republish is owned by MqttService itself
/// (it fires on ConnectedAsync). This worker only handles state values.
/// </summary>
public sealed class SensorsWorker : IAsyncDisposable
{
    private readonly IApplicationStateService _state;
    private readonly IMqttService _mqtt;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public SensorsWorker(IApplicationStateService state, IMqttService mqtt)
    {
        _state = state;
        _mqtt = mqtt;
    }

    public void Start()
    {
        if (_loop is { IsCompleted: false }) return;
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => RunAsync(_cts.Token));
        Log.Information("[SENSORS] Worker started");
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        if (_loop != null)
        {
            try { await _loop.ConfigureAwait(false); } catch { /* shutting down */ }
        }
        _cts?.Dispose();
    }

    private async Task RunAsync(CancellationToken ct)
    {
        // First run after connect: publish state even when the sensor's value hasn't changed,
        // so HA sees something other than 'unknown' immediately.
        var firstRun = true;
        var lastAvailabilityAt = DateTime.MinValue;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(750), ct);

                if (_mqtt.ConnectionState != MqttConnectionState.Connected)
                {
                    // Mark so that the next time we're connected we re-publish ground-truth state.
                    firstRun = true;
                    continue;
                }

                // Heartbeat availability every 30s — sensors' own state messages already imply
                // availability, but HA wants the dedicated topic kept warm for `availability_topic`.
                if ((DateTime.Now - lastAvailabilityAt).TotalSeconds > 30)
                {
                    await _mqtt.AnnounceAvailabilityAsync(true).ConfigureAwait(false);
                    lastAvailabilityAt = DateTime.Now;
                }

                foreach (var sensor in _state.SingleValueSensors.ToList())
                {
                    if (ct.IsCancellationRequested) break;
                    if (_mqtt.ConnectionState != MqttConnectionState.Connected) break;
                    try { await sensor.PublishStateAsync(!firstRun).ConfigureAwait(false); }
                    catch (Exception ex) { Log.Error(ex, "[SENSORS] PublishState failed for {name}", sensor.Name); }
                }

                foreach (var sensor in _state.MultiValueSensors.ToList())
                {
                    if (ct.IsCancellationRequested) break;
                    if (_mqtt.ConnectionState != MqttConnectionState.Connected) break;
                    try { await sensor.PublishStatesAsync(!firstRun).ConfigureAwait(false); }
                    catch (Exception ex) { Log.Error(ex, "[SENSORS] PublishStates failed for {name}", sensor.Name); }
                }

                if (firstRun) firstRun = false;
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Log.Error(ex, "[SENSORS] Worker iteration failed: {err}", ex.Message);
            }
        }

        Log.Information("[SENSORS] Worker stopped");
    }
}
