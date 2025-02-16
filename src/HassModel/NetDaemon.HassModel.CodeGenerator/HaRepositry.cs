using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NetDaemon.Client;
using NetDaemon.Client.Extensions;
using NetDaemon.Client.HomeAssistant.Extensions;
using NetDaemon.Client.HomeAssistant.Model;
using NetDaemon.Client.Settings;

namespace NetDaemon.HassModel.CodeGenerator;

internal static class HaRepositry
{
    public record HaData(IReadOnlyCollection<HassState> states, JsonElement? servicesMetaData);

    public static async Task<HaData> GetHaData(IHomeAssistantClient client, HomeAssistantSettings homeAssistantSettings)
    {
        Console.WriteLine($"Connecting to Home Assistant at {homeAssistantSettings.Host}:{homeAssistantSettings.Port}");


        var connection = await client.ConnectAsync(
            homeAssistantSettings.Host, 
            homeAssistantSettings.Port,
            homeAssistantSettings.Ssl,
            homeAssistantSettings.Token,
            CancellationToken.None).ConfigureAwait(false);
        
        await using (connection.ConfigureAwait(false))
        {
            var services = await connection.GetServicesAsync(CancellationToken.None).ConfigureAwait(false);
            var states = await connection.GetStatesAsync(CancellationToken.None).ConfigureAwait(false);

            return new HaData(states!, services);
        }
    }

}
