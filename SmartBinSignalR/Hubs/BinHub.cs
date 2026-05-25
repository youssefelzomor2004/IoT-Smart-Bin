using Microsoft.AspNetCore.SignalR;

public class BinHub : Hub
{
    public async Task SendBinUpdate(int level, bool hand, string state)
    {
        await Clients.All.SendAsync("ReceiveBinUpdate", level, hand, state);
    }
}