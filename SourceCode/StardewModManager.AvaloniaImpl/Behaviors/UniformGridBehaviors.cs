namespace StardewModManager.AvaloniaImpl.Behaviors;

using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

public class UniformGridBehaviors
{
    private static readonly ConditionalWeakTable<Control, IDisposable> s_subscriptionsStore = [];

    public static readonly AttachedProperty<double> MinColumWidthProperty =
        AvaloniaProperty.RegisterAttached<UniformGridBehaviors, UniformGrid, double>(
            "MinColumWidth",
            defaultValue: double.NaN,
            validate: v => v is double.NaN or >= 0
        );

    static UniformGridBehaviors()
    {
        MinColumWidthProperty.Changed.Subscribe(
            args =>
            {
                if (args.Sender is not UniformGrid uniformGrid) return;

                OnMinColumnWidthChanged(uniformGrid, args.NewValue.Value);
            }
        );
    }

    public static void SetMinColumWidth(UniformGrid control, double value)
    {
        control.SetValue(MinColumWidthProperty, value);
        
    }
    
    public static double GetMinColumWidth(UniformGrid control)
    {
        return control.GetValue(MinColumWidthProperty);
    }

    private static void OnMinColumnWidthChanged(UniformGrid control, double value)
    {
        if (value is not double.NaN && !s_subscriptionsStore.TryGetValue(control, out _))
        {
            var subscription = control.GetObservable(MinColumWidthProperty)
                .CombineLatest(control.GetObservable(Visual.BoundsProperty))
                .Subscribe(args => UpdateColumnsCount(control, args.First));

            s_subscriptionsStore.Add(control, subscription);

            return;
        }

        if (value is not double.NaN || !s_subscriptionsStore.TryGetValue(control, out var existingSubscription)) return;

        existingSubscription.Dispose();
        s_subscriptionsStore.Remove(control);
    }

    private static void UpdateColumnsCount(UniformGrid control, double minColumnWidth)
    {
        var floatColumns = control.Bounds.Width / minColumnWidth;

        if (floatColumns < 0) floatColumns = 1;

        var columns = (int)floatColumns;

        if (control.Columns == columns) return;

        control.Columns = columns;
    }
}