using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace USBreeze.App;

public sealed class AnimatedSpeed : TextBlock
{
    public static readonly DependencyProperty AmountProperty = DependencyProperty.Register(
        nameof(Amount), typeof(double), typeof(AnimatedSpeed), new PropertyMetadata(0d, OnAmount));
    public double Amount { get => (double)GetValue(AmountProperty); set => SetValue(AmountProperty, value); }
    private static void OnAmount(DependencyObject sender, DependencyPropertyChangedEventArgs e) => ((AnimatedSpeed)sender).RefreshText();
    public void RefreshText() => Text = Amount.ToString("F1", Texts.Current.Culture);
    public void Reset() { BeginAnimation(AmountProperty, null); Amount = 0; Text = "—"; }
    public void SetSpeed(double value, bool animate = true)
    {
        if (!double.IsFinite(value) || value < 0) return;
        var from = Amount;
        BeginAnimation(AmountProperty, null);
        Amount = value;
        if (animate && SystemParameters.ClientAreaAnimation)
            BeginAnimation(AmountProperty, new DoubleAnimation(from, value, TimeSpan.FromMilliseconds(240))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
    }
}
