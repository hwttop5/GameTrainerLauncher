using GameTrainerLauncher.Core.Entities;
using GameTrainerLauncher.UI.Services;
using GameTrainerLauncher.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GameTrainerLauncher.UI.Views;

public partial class MyGamesPage : Page
{
    private readonly IMyGamesRefreshService _refreshService;
    private Game? _draggedGame;
    private Point _dragStartPoint;

    public MyGamesPage(MyGamesViewModel viewModel, IMyGamesRefreshService refreshService)
    {
        InitializeComponent();
        DataContext = viewModel;
        _refreshService = refreshService;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        GamesList.PreviewMouseLeftButtonDown += OnGamesListPreviewMouseLeftButtonDown;
        GamesList.PreviewMouseMove += OnGamesListPreviewMouseMove;
        GamesList.DragOver += OnGamesListDragOver;
        GamesList.Drop += OnGamesListDrop;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _refreshService.Register(() =>
        {
            if (DataContext is MyGamesViewModel vm)
                _ = vm.LoadGamesCommand.ExecuteAsync(null);
        });
        if (DataContext is MyGamesViewModel vm)
            _ = vm.LoadGamesCommand.ExecuteAsync(null);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _refreshService.Unregister();
    }

    private void OnGamesListPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var item = FindAncestor<ListViewItem>(e.OriginalSource as DependencyObject);
        if (item != null && item.DataContext is Game game)
        {
            _draggedGame = game;
            _dragStartPoint = e.GetPosition(null);
        }
        else
            _draggedGame = null;
    }

    private void OnGamesListPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _draggedGame == null)
            return;
        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _dragStartPoint.X) < 4 && Math.Abs(pos.Y - _dragStartPoint.Y) < 4)
            return;
        _ = DragDrop.DoDragDrop(GamesList, _draggedGame, DragDropEffects.Move);
        _draggedGame = null;
    }

    private void OnGamesListDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(Game)))
            e.Effects = DragDropEffects.Move;
        else
            e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void OnGamesListDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not MyGamesViewModel vm || e.Data.GetData(typeof(Game)) is not Game dragged)
            return;
        var targetItem = FindAncestor<ListViewItem>(e.OriginalSource as DependencyObject);
        int newIndex = targetItem != null && GamesList.ItemContainerGenerator.IndexFromContainer(targetItem) >= 0
            ? GamesList.ItemContainerGenerator.IndexFromContainer(targetItem)
            : Math.Max(0, vm.Games.Count - 1);
        var oldIndex = vm.Games.IndexOf(dragged);
        if (oldIndex >= 0 && newIndex >= 0 && oldIndex != newIndex)
            vm.MoveGameByIndex(oldIndex, newIndex);
        e.Handled = true;
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T t)
                return t;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
