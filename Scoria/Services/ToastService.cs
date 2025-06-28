using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Media;
using Avalonia.Threading;

namespace Scoria.Services
{
    /// <summary>
    /// Tiny helper that shows a transient “toast” notification
    /// (bottom-right overlay) inside any Avalonia <see cref="Panel"/>.
    /// <para/>
    /// Currently hard-wired to display the message <c>"Saved: …"</c> and then:
    /// <list type="number">
    ///   <item>Wait 2 s.</item>
    ///   <item>Fade opacity from 1 → 0 over 5 s (via <see cref="DoubleTransition"/>).</item>
    ///   <item>Remove the <see cref="Popup"/> from the visual tree.</item>
    /// </list>
    /// </summary>
    public class ToastService : IToastService
    {
        /*──────────────────────────  State  ──────────────────────────*/
        private readonly Panel host;
        
        /*──────────────────────────  Ctor  ──────────────────────────*/
        
        /// <summary>Creates a service bound to a specific visual host panel.</summary>
        public ToastService(Panel _host) => host = _host;
        
        /*──────────────────────────  API  ───────────────────────────*/
        
        /// <inheritdoc/>
        public void Show(string _message)
        {
            // 1) Create and configure Popup content and configuration
            var border = new Border
            {
                Background   = Brushes.DimGray,
                Opacity      = 1.0,
                CornerRadius = new CornerRadius(4),
                Padding      = new Thickness(8),
                Child        = new TextBlock
                {
                    Text       = $"Saved: {_message}",
                    Foreground = Brushes.White,
                },
                // 2) Add a fade transition on Opacity
                Transitions = new Transitions
                {
                    new DoubleTransition
                    {
                        Property = Border.OpacityProperty,
                        Duration = TimeSpan.FromSeconds(5)
                    }
                }
            };

            // 3) Create the popup
            var popup = new Popup
            {
                PlacementTarget   = host,
                PlacementMode     = PlacementMode.AnchorAndGravity,
        
                // Anchor at the bottom-center of the target
                PlacementAnchor   = PopupAnchor.BottomRight,
                // Gravity = push *down* from that anchor
                PlacementGravity  = PopupGravity.Bottom,

                // nudge it a few pixels so it’s within the application window
                // TODO change this so its dynamic, currently its fixed size, so larger file names would exceed the window.
                HorizontalOffset  = -100,
                VerticalOffset    = -40,

                // light dismiss if you tap anywhere else
                //IsLightDismissEnabled = true,

                Child             = border
            };

            // 4) Display the popup
            host.Children.Add(popup);
            popup.IsOpen = true;

            // 5) After 2s, kick off the fade, then remove after the transition
            DispatcherTimer fadeTimer = null;
            fadeTimer = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, (_, __) =>
            {
                fadeTimer.Stop();

                // trigger fade-out
                border.Opacity = 0;

                // remove once the 5s transition has finished TODO magic number, also probably want this in a settings window.
                DispatcherTimer removeTimer = null;
                removeTimer = new DispatcherTimer(TimeSpan.FromSeconds(5), DispatcherPriority.Background, (_, __2) =>
                {
                    removeTimer.Stop();
                    popup.IsOpen = false;
                    host.Children.Remove(popup);
                });
                removeTimer.Start();
            });
            fadeTimer.Start();
        }
    }
}
