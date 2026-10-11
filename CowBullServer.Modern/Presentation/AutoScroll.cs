using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace CowBullServer.Modern.Presentation;

public static class AutoScroll
{
    public static readonly DependencyProperty ToEndProperty =
        DependencyProperty.RegisterAttached(
            "ToEnd",
            typeof(bool),
            typeof(AutoScroll),
            new PropertyMetadata(false, OnToEndChanged));

    public static bool GetToEnd(DependencyObject element) =>
        (bool)element.GetValue(ToEndProperty);

    public static void SetToEnd(DependencyObject element, bool value) =>
        element.SetValue(ToEndProperty, value);

    private static void OnToEndChanged(DependencyObject sender, DependencyPropertyChangedEventArgs eventArgs)
    {
        if (sender is not ListBox listBox || eventArgs.NewValue is not true)
        {
            return;
        }

        if (listBox.Items is INotifyCollectionChanged collection)
        {
            collection.CollectionChanged += (_, _) => ScrollToEnd(listBox);
        }
    }

    private static void ScrollToEnd(ListBox listBox)
    {
        if (listBox.Items.Count == 0)
        {
            return;
        }

        listBox.ScrollIntoView(listBox.Items[^1]);
    }
}
