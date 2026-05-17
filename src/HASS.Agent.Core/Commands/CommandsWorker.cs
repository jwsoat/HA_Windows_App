using HASS.Agent.Core.Mqtt;
using HASS.Agent.Core.Services;
using Serilog;

namespace HASS.Agent.Core.Commands;

/// <summary>
/// Background loop that drives command publishing: keeps each command subscribed to its MQTT
/// topics (so HA-side button/switch presses reach us) and pushes state for switch-style commands.
/// Autodiscovery republish is owned by MqttService on connect — this worker handles ongoing state.
/// </summary>
public sealed class CommandsWorker : IAsyncDisposable
{
    private readonly IApplicationStateService _state;
    private readonly IMqttService _mqtt;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private readonly HashSet<string> _subscribed = new();

    public CommandsWorker(IApplicationStateService state, IMqttService mqtt)
    {
        _state = state;
        _mqtt = mqtt;
    }

    public void Start()
    {
        if (_loop is { IsCompleted: false }) return;
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => RunAsync(_cts.Token));
        Log.Information("[COMMANDS] Worker started");
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
        var firstRun = true;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(firstRun ? TimeSpan.FromSeconds(1) : TimeSpan.FromMilliseconds(750), ct);

                if (_mqtt.ConnectionState != MqttConnectionState.Connected)
                {
                    // Reconnect will wipe broker-side subscription state; track it locally so we
                    // resubscribe everything next time we're up.
                    _subscribed.Clear();
                    continue;
                }

                firstRun = false;

                foreach (var cmd in _state.Commands.ToList())
                {
                    if (ct.IsCancellationRequested) break;
                    if (_mqtt.ConnectionState != MqttConnectionState.Connected) break;

                    // (Re)subscribe each command's topics once per session.
                    if (!_subscribed.Contains(cmd.Id.ToString()))
                    {
                        try
                        {
                            await _mqtt.SubscribeAsync(cmd).ConfigureAwait(false);
                            _subscribed.Add(cmd.Id.ToString());
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "[COMMANDS] Subscribe failed for {name}", cmd.Name);
                        }
                    }

                    // Switch/lock/light entities need state pushed back so HA reflects ON/OFF.
                    try { await cmd.PublishStateAsync().ConfigureAwait(false); }
                    catch (Exception ex) { Log.Error(ex, "[COMMANDS] PublishState failed for {name}", cmd.Name); }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Log.Error(ex, "[COMMANDS] Worker iteration failed: {err}", ex.Message);
            }
        }

        Log.Information("[COMMANDS] Worker stopped");
    }
}
