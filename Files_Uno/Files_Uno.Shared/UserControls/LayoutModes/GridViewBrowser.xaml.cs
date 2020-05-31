﻿using Files.Filesystem;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Interaction = Files.Interacts.Interaction;

namespace Files
{
    public sealed partial class GridViewBrowser : BaseLayout
    {
        public GridViewBrowser()
        {
            this.InitializeComponent();

            App.AppSettings.LayoutModeChangeRequested += AppSettings_LayoutModeChangeRequested;

            SetItemTemplate(); // Set ItemTemplate
        }

        private void AppSettings_LayoutModeChangeRequested(object sender, EventArgs e)
        {
            SetItemTemplate(); // Set ItemTemplate
        }

        private void SetItemTemplate()
        {
            FileList.ItemTemplate = (App.AppSettings.LayoutMode == 1) ? StaticResources.TilesBrowserTemplate : StaticResources.GridViewBrowserTemplate; // Choose Template

            // Set GridViewSize event handlers
            if (App.AppSettings.LayoutMode == 1)
            {
                App.AppSettings.GridViewSizeChangeRequested -= AppSettings_GridViewSizeChangeRequested;
            }
            else if (App.AppSettings.LayoutMode == 2)
            {
                _iconSize = UpdateThumbnailSize(); // Get icon size for jumps from other layouts directly to a grid size
                App.AppSettings.GridViewSizeChangeRequested += AppSettings_GridViewSizeChangeRequested;
            }
        }

        public override void SetSelectedItemOnUi(ListedItem item)
        {
            ClearSelection();
            FileList.SelectedItems.Add(item);
        }

        public override void SetSelectedItemsOnUi(List<ListedItem> items)
        {
            ClearSelection();

            foreach (ListedItem item in items)
            {
                FileList.SelectedItems.Add(item);
            }
        }

        public override void SelectAllItems()
        {
            ClearSelection();
            FileList.SelectAll();
        }

        public override void InvertSelection()
        {
            List<ListedItem> allItems = FileList.Items.Cast<ListedItem>().ToList();
            List<ListedItem> newSelectedItems = allItems.Except(SelectedItems).ToList();

            SetSelectedItemsOnUi(newSelectedItems);
        }

        public override void ClearSelection()
        {
            FileList.SelectedItems.Clear();
        }

        public override void SetDragModeForItems()
        {
            foreach (ListedItem listedItem in FileList.Items)
            {
                GridViewItem gridViewItem = FileList.ContainerFromItem(listedItem) as GridViewItem;

                if (gridViewItem != null)
                {
                    List<Grid> grids = new List<Grid>();
                    Interaction.FindChildren(grids, gridViewItem);
                    var rootItem = grids.Find(x => x.Tag?.ToString() == "ItemRoot");
                    rootItem.CanDrag = SelectedItems.Contains(listedItem);
                }
            }
        }

        public override void ScrollIntoView(ListedItem item)
        {
            FileList.ScrollIntoView(item);
        }

        public override int GetSelectedIndex()
        {
            return FileList.SelectedIndex;
        }

        public override void FocusSelectedItems()
        {
            FileList.ScrollIntoView(FileList.Items.Last());
        }

        private void StackPanel_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var parentContainer = Interaction.FindParent<GridViewItem>(e.OriginalSource as DependencyObject);
            if (FileList.SelectedItems.Contains(FileList.ItemFromContainer(parentContainer)))
            {
                return;
            }
            // The following code is only reachable when a user RightTapped an unselected row
            SetSelectedItemOnUi(FileList.ItemFromContainer(parentContainer) as ListedItem);
        }

        private void GridViewBrowserViewer_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.GetCurrentPoint(sender as Page).Properties.IsLeftButtonPressed)
            {
                ClearSelection();
            }
        }

        private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedItems = FileList.SelectedItems.Cast<ListedItem>().ToList();
        }

        private ListedItem renamingItem;

        public override void StartRenameItem()
        {
            renamingItem = SelectedItem;
            GridViewItem gridViewItem = FileList.ContainerFromItem(renamingItem) as GridViewItem;
            // Handle layout differences between tiles browser and photo album
            StackPanel stackPanel = (App.AppSettings.LayoutMode == 2)
                ? (gridViewItem.ContentTemplateRoot as Grid).Children[1] as StackPanel
                : (((gridViewItem.ContentTemplateRoot as Grid).Children[0] as StackPanel).Children[1] as Grid).Children[0] as StackPanel;
            TextBlock textBlock = stackPanel.Children[0] as TextBlock;
            TextBox textBox = stackPanel.Children[1] as TextBox;
            int extensionLength = renamingItem.FileExtension?.Length ?? 0;

            textBlock.Visibility = Visibility.Collapsed;
            textBox.Visibility = Visibility.Visible;
            textBox.Focus(FocusState.Pointer);
            textBox.LostFocus += RenameTextBox_LostFocus;
            textBox.KeyDown += RenameTextBox_KeyDown;
            textBox.Select(0, renamingItem.ItemName.Length - extensionLength);
            isRenamingItem = true;
        }

        public override void ResetItemOpacity()
        {
            foreach (ListedItem listedItem in FileList.Items)
            {
                List<Grid> itemContentGrids = new List<Grid>();
                GridViewItem gridViewItem = FileList.ContainerFromItem(listedItem) as GridViewItem;
                if (gridViewItem == null)
                {
                    return;
                }
                Interaction.FindChildren<Grid>(itemContentGrids, gridViewItem);
                var imageOfItem = itemContentGrids.Find(x => x.Tag?.ToString() == "ItemImage");
                imageOfItem.Opacity = 1;
            }
        }

        public override void SetItemOpacity(ListedItem item)
        {
            GridViewItem itemToDimForCut = (GridViewItem)FileList.ContainerFromItem(item);
            List<Grid> itemContentGrids = new List<Grid>();
            Interaction.FindChildren(itemContentGrids, itemToDimForCut);
            var imageOfItem = itemContentGrids.Find(x => x.Tag?.ToString() == "ItemImage");
            imageOfItem.Opacity = 0.4;
        }

        private void RenameTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Escape)
            {
                TextBox textBox = sender as TextBox;
                textBox.LostFocus -= RenameTextBox_LostFocus;
                EndRename(textBox);
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Enter)
            {
                TextBox textBox = sender as TextBox;
                CommitRename(textBox);
                e.Handled = true;
            }
        }

        private void RenameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = e.OriginalSource as TextBox;
            CommitRename(textBox);
        }

        private async void CommitRename(TextBox textBox)
        {
            EndRename(textBox);
            var selectedItem = renamingItem;
            string currentName = selectedItem.ItemName;
            string newName = textBox.Text;

            if (newName == null)
                return;

            await App.CurrentInstance.InteractionOperations.RenameFileItem(selectedItem, currentName, newName);
        }

        private void EndRename(TextBox textBox)
        {
            StackPanel parentPanel = textBox.Parent as StackPanel;
            TextBlock textBlock = parentPanel.Children[0] as TextBlock;
            textBox.Visibility = Visibility.Collapsed;
            textBlock.Visibility = Visibility.Visible;
            textBox.LostFocus -= RenameTextBox_LostFocus;
            textBox.KeyDown += RenameTextBox_KeyDown;
            isRenamingItem = false;
        }

        private void FileList_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter && !e.KeyStatus.IsMenuKeyDown)
            {
                if (!isRenamingItem)
                {
                    App.CurrentInstance.InteractionOperations.List_ItemClick(null, null);
                    e.Handled = true;
                }
            }
            else if (e.Key == VirtualKey.Enter && e.KeyStatus.IsMenuKeyDown)
            {
                AssociatedInteractions.ShowPropertiesButton_Click(null, null);
            }
        }

        protected override void Page_CharacterReceived(CoreWindow sender, CharacterReceivedEventArgs args)
        {
            if (App.CurrentInstance != null)
            {
                if (App.CurrentInstance.CurrentPageType == typeof(GridViewBrowser) && !isRenamingItem)
                {
#if !__MACOS__
                    var focusedElement = FocusManager.GetFocusedElement(XamlRoot) as FrameworkElement;
                    if (focusedElement is TextBox)
                    {
                        return;
                    }

                    base.Page_CharacterReceived(sender, args);
                    FileList.Focus(FocusState.Keyboard);
#endif
                }
            }
        }

        private async void Grid_EffectiveViewportChanged(FrameworkElement sender, EffectiveViewportChangedEventArgs args)
        {
            if (sender.DataContext != null && (!(sender.DataContext as ListedItem).ItemPropertiesInitialized) && (args.BringIntoViewDistanceX < sender.ActualHeight))
            {
                await Windows.UI.Xaml.Window.Current.CoreWindow.Dispatcher.RunIdleAsync((e) =>
                {
                    App.CurrentInstance.ViewModel.LoadExtendedItemProperties(sender.DataContext as ListedItem, _iconSize);
                    (sender.DataContext as ListedItem).ItemPropertiesInitialized = true;
                });
                (sender as UIElement).CanDrag = FileList.SelectedItems.Contains(sender.DataContext as ListedItem); // Update CanDrag
            }
        }

        protected override ListedItem GetItemFromElement(object element)
        {
            FrameworkElement gridItem = element as FrameworkElement;
            return gridItem.DataContext as ListedItem;
        }

        private void FileListGridItem_DataContextChanged(object sender, DataContextChangedEventArgs e)
        {
            InitializeDrag(sender as UIElement);
        }

        private void FileListGridItem_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.KeyModifiers == VirtualKeyModifiers.Control)
            {
                var listedItem = (sender as Grid).DataContext as ListedItem;
                if (FileList.SelectedItems.Contains(listedItem))
                {
                    FileList.SelectedItems.Remove(listedItem);
                    // Prevent issues arising caused by the default handlers attempting to select the item that has just been deselected by ctrl + click
                    e.Handled = true;
                }
            }
            else if (e.GetCurrentPoint(sender as UIElement).Properties.IsLeftButtonPressed)
            {
                var listedItem = (sender as Grid).DataContext as ListedItem;

                if (!FileList.SelectedItems.Contains(listedItem))
                {
                    SetSelectedItemOnUi(listedItem);
                }
            }
        }

        private uint _iconSize = UpdateThumbnailSize();

        private static uint UpdateThumbnailSize()
        {
            if (App.AppSettings.LayoutMode == 1 || App.AppSettings.GridViewSize < 200)
                return 80; // Small thumbnail
            else if (App.AppSettings.GridViewSize < 275)
                return 120; // Medium thumbnail
            else if (App.AppSettings.GridViewSize < 325)
                return 160; // Large thumbnail
            else
                return 240; // Extra large thumbnail
        }

        private void AppSettings_GridViewSizeChangeRequested(object sender, EventArgs e)
        {
            var iconSize = UpdateThumbnailSize(); // Get new icon size

            // Prevents reloading icons when the icon size hasn't changed
            if (iconSize != _iconSize)
            {
                _iconSize = iconSize; // Update icon size before refreshing
                NavigationActions.Refresh_Click(null, null); // Refresh icons
            }
            else
                _iconSize = iconSize; // Update icon size
        }

        public bool IsSelectedItemImage => App.InteractionViewModel.IsSelectedItemImage;

        public bool PSIsEnabled
        {
            get => App.PS.IsEnabled;
            set => App.PS.IsEnabled = value;
        }

        public int GridViewSize => App.AppSettings.GridViewSize;

        public void ToggleQuickLook()
        {
            App.CurrentInstance.InteractionOperations.ToggleQuickLook();
        }

        public void GridViewSizeIncrease_(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            App.CurrentInstance.ContentPage.GridViewSizeIncrease(sender, args);
        }

        public void GridViewSizeDecrease_(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            App.CurrentInstance.ContentPage.GridViewSizeDecrease(sender, args);
        }

        public void List_ItemClick(object sender, DoubleTappedRoutedEventArgs e)
        {
            App.CurrentInstance.InteractionOperations.List_ItemClick(sender, e);
        }

        public void PasteItem_ClickAsync(object sender, RoutedEventArgs e)
        {
            App.CurrentInstance.InteractionOperations.PasteItem_ClickAsync(sender, e);
        }

        public void OpenDirectoryInTerminal(object sender, RoutedEventArgs e)
        {
            App.CurrentInstance.InteractionOperations.OpenDirectoryInTerminal(sender, e);
        }

        public void NewFolder_Click(object sender, RoutedEventArgs e)
        {
            App.CurrentInstance.InteractionOperations.NewFolder_Click(sender, e);
        }
        public void NewBitmapImage_Click(object sender, RoutedEventArgs e)
        {
            App.CurrentInstance.InteractionOperations.NewBitmapImage_Click(sender, e);
        }
        public void NewTextDocument_Click(object sender, RoutedEventArgs e)
        {
            App.CurrentInstance.InteractionOperations.NewTextDocument_Click(sender, e);
        }
        public void ShowFolderPropertiesButton_Click(object sender, RoutedEventArgs e)
        {
            App.CurrentInstance.InteractionOperations.ShowFolderPropertiesButton_Click(sender, e);
        }
        public void ExtractItems_Click(object sender, RoutedEventArgs e)
        {
            App.CurrentInstance.InteractionOperations.ExtractItems_Click(sender, e);
        }

        public void OpenItem_Click(object sender, RoutedEventArgs e)
        {
            App.CurrentInstance.InteractionOperations.OpenItem_Click(sender, e);
        }

        public void OpenDirectoryInNewTab_Click(object sender, RoutedEventArgs e)
        {
            App.CurrentInstance.InteractionOperations.OpenDirectoryInNewTab_Click(sender, e);
        }

        public void OpenInNewWindowItem_Click(object sender, RoutedEventArgs e)
        {
            App.CurrentInstance.InteractionOperations.OpenInNewWindowItem_Click(sender, e);
        }

        public void SetAsDesktopBackgroundItem_Click(object sender, RoutedEventArgs e)
        {
            App.CurrentInstance.InteractionOperations.SetAsDesktopBackgroundItem_Click(sender, e);
        }

        public void SetAsLockscreenBackgroundItem_Click(object sender, RoutedEventArgs e)
        {
            App.CurrentInstance.InteractionOperations.SetAsLockscreenBackgroundItem_Click(sender, e);
        }

        public void ShareItem_Click(object sender, RoutedEventArgs e)
        {
            App.CurrentInstance.InteractionOperations.ShareItem_Click(sender, e);
        }

        public void CutItem_Click(object sender, RoutedEventArgs e)
        {
            App.CurrentInstance.InteractionOperations.CutItem_Click(sender, e);
        }

        public void CopyItem_ClickAsync(object sender, RoutedEventArgs e)
        {
            App.CurrentInstance.InteractionOperations.CopyItem_ClickAsync(sender, e);
        }

        public void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            App.CurrentInstance.InteractionOperations.DeleteItem_Click(sender, e);
        }

        public void RenameItem_Click(object sender, RoutedEventArgs e)
        {
            App.CurrentInstance.InteractionOperations.RenameItem_Click(sender, e);
        }

        public void PinItem_Click(object sender, RoutedEventArgs e)
        {
            App.CurrentInstance.InteractionOperations.PinItem_Click(sender, e);
        }

        public void ShowPropertiesButton_Click(object sender, RoutedEventArgs e)
        {
            App.CurrentInstance.InteractionOperations.ShowPropertiesButton_Click(sender, e);
        }

        public void Refresh_Click(object sender, RoutedEventArgs e)
        {
            NavigationActions.Refresh_Click(sender, e);
        }
    }

}