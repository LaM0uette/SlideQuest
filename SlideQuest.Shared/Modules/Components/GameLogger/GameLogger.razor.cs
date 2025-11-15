using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Components.GameLogger;

public class GameLoggerPresenter : ComponentBase
{
    #region Statements
    
    private const int MAX_LOG_COUNT = 100;

    protected ElementReference GameLoggerTerminalRef;
    protected readonly List<string> Logs = [];

    [Inject] private IJSRuntime _jsRuntime { get; set; } = null!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("sqScrollToBottom", GameLoggerTerminalRef);
        }
        catch
        {
            // ignore
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    #endregion

    #region Methods

    public void Log(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) 
            return;
        
        Logs.Add(message);
        
        if (Logs.Count > MAX_LOG_COUNT)
        {
            Logs.RemoveAt(0);
        }
        
        StateHasChanged();
    }

    #endregion
}