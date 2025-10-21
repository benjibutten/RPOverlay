using System.Collections.Generic;

namespace RPOverlay.Core.Models;

public sealed class OverlayConfig
{
    public string Hotkey { get; set; } = "F9";
    public List<OverlayButton> Buttons { get; set; } = new();
    public WindowSettings? Window { get; set; }

    public static OverlayConfig CreateDefault() => new()
    {
        Hotkey = "F9",
        Buttons = new List<OverlayButton>
        {
            new OverlayButton
            {
                Label = "Ta fram ID",
                Text = "/me tar fram sitt ID-kort och visar upp det"
            },
            new OverlayButton
            {
                Label = "Behandlar",
                Text = "/me påbörjar behandling och kontrollerar puls"
            },
            new OverlayButton
            {
                Label = "Förband",
                Text = "/me tar fram ett förband och lindar runt såret"
            },
            new OverlayButton
            {
                Label = "Kommunicerar",
                Text = "/do Patienten svarar svagt men är vid medvetande"
            }
        }
    };
}

public sealed class OverlayButton
{
    public string Label { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public sealed class WindowSettings
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; } = 320;
    public double Height { get; set; } = 500;
}
