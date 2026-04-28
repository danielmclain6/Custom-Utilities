using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CustomUtils.Utilities.Reminders;

public partial class RemindersView : UserControl
{
    public RemindersView()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
        App.Scheduler.Changed += () => Dispatcher.Invoke(Refresh);
    }

    private void Refresh()
    {
        var all = App.Store.All().OrderBy(r => r.FireAt).ToList();
        var now = DateTime.Now;

        var upcoming = all.Where(r => r.Status == ReminderStatus.Pending && r.FireAt > now).ToList();
        var past = all.Where(r => r.Status != ReminderStatus.Pending || r.FireAt <= now)
                       .OrderByDescending(r => r.FireAt)
                       .ToList();

        UpcomingList.Items.Clear();
        foreach (var r in upcoming) UpcomingList.Items.Add(BuildRow(r, isPast: false));

        PastList.Items.Clear();
        foreach (var r in past) PastList.Items.Add(BuildRow(r, isPast: true));

        EmptyHint.Visibility = all.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private FrameworkElement BuildRow(Reminder r, bool isPast)
    {
        var card = new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 0, 0, 8),
            BorderThickness = new Thickness(1),
            Tag = r.Id,
        };
        card.SetResourceReference(Border.BackgroundProperty, "CardBg");
        card.SetResourceReference(Border.BorderBrushProperty, "CardBorder");

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var stack = new StackPanel();
        var title = new TextBlock { Text = string.IsNullOrWhiteSpace(r.Title) ? "(no title)" : r.Title, FontSize = 14, FontWeight = FontWeights.SemiBold };
        var sub = new TextBlock
        {
            Text = FormatWhen(r),
            FontSize = 12,
            Margin = new Thickness(0, 3, 0, 0),
        };
        sub.SetResourceReference(TextBlock.ForegroundProperty, "MutedFg");
        stack.Children.Add(title);
        stack.Children.Add(sub);
        if (!string.IsNullOrWhiteSpace(r.Note))
        {
            var noteTb = new TextBlock
            {
                Text = r.Note,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0),
            };
            noteTb.SetResourceReference(TextBlock.ForegroundProperty, "MutedFg");
            stack.Children.Add(noteTb);
        }
        Grid.SetColumn(stack, 0);
        grid.Children.Add(stack);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        if (!isPast)
        {
            var editBtn = new Button { Content = "Edit", Style = (Style)Application.Current.Resources["GhostButton"], Margin = new Thickness(0, 0, 6, 0) };
            editBtn.Click += (_, _) => OpenEdit(r);
            actions.Children.Add(editBtn);
        }

        var deleteBtn = new Button { Content = "Delete", Style = (Style)Application.Current.Resources["GhostButton"] };
        deleteBtn.Click += (_, _) =>
        {
            App.Scheduler.Cancel(r.Id);
            App.Store.Delete(r.Id);
            Refresh();
        };
        actions.Children.Add(deleteBtn);

        Grid.SetColumn(actions, 1);
        grid.Children.Add(actions);

        card.Child = grid;
        return card;
    }

    private static string FormatWhen(Reminder r)
    {
        var label = r.Status switch
        {
            ReminderStatus.Fired => "Fired",
            ReminderStatus.Dismissed => "Dismissed",
            _ => r.FireAt > DateTime.Now ? "Fires" : "Due",
        };
        return $"{label} · {r.FireAt:ddd, MMM d  h:mm tt}";
    }

    private void NewButton_Click(object sender, RoutedEventArgs e) => OpenEdit(null);

    private void OpenEdit(Reminder? existing)
    {
        var edit = new ReminderEditView(existing);
        edit.Done += saved =>
        {
            EditPaneHost.Content = null;
            EditPaneHost.Visibility = Visibility.Collapsed;
            ListPane.Visibility = Visibility.Visible;
            Refresh();
        };
        EditPaneHost.Content = edit;
        ListPane.Visibility = Visibility.Collapsed;
        EditPaneHost.Visibility = Visibility.Visible;
    }

    public void FocusReminder(Guid id)
    {
        // For now just refresh — the row is visible in the list. Could add scroll-into-view later.
        Refresh();
    }
}
