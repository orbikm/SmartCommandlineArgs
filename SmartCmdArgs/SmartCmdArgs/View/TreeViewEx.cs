﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;

using SmartCmdArgs.ViewModel;

namespace SmartCmdArgs.View
{
    public class TreeViewEx : TreeView
    {
        protected override DependencyObject GetContainerForItemOverride() => new TreeViewItemEx(this);
        protected override bool IsItemItsOwnContainerOverride(object item) => item is TreeViewItemEx;

        // taken from https://stackoverflow.com/questions/459375/customizing-the-treeview-to-allow-multi-select

        // Used in shift selections
        private TreeViewItemEx _lastItemSelected;

        public static readonly DependencyProperty IsItemSelectedProperty =
            DependencyProperty.RegisterAttached("IsItemSelected", typeof(bool), typeof(TreeViewEx));

        public static void SetIsItemSelected(UIElement element, bool value)
        {
            element.SetValue(IsItemSelectedProperty, value);
        }
        public static bool GetIsItemSelected(UIElement element)
        {
            return (bool)element.GetValue(IsItemSelectedProperty);
        }
        

        private static bool IsCtrlPressed => (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

        private static bool IsShiftPressed => (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

        public IEnumerable<TreeViewItemEx> SelectedTreeViewItems => GetTreeViewItems(this, true).Where(GetIsItemSelected);
        public IEnumerable<CmdBase> SelectedItems => SelectedTreeViewItems.Select(treeViewItem => treeViewItem.Item);
        
        
        public void ChangedFocusedItem(TreeViewItemEx item)
        {
            if (Keyboard.IsKeyDown(Key.Up)
                || Keyboard.IsKeyDown(Key.Down)
                || Keyboard.IsKeyDown(Key.Left)
                || Keyboard.IsKeyDown(Key.Right)
                || Keyboard.IsKeyDown(Key.Prior)
                || Keyboard.IsKeyDown(Key.Next)
                || Keyboard.IsKeyDown(Key.End)
                || Keyboard.IsKeyDown(Key.Home))
            {
                SelectedItemChangedInternal(item);
            }
        }

        private TreeViewItemEx _lastMouseDownTargetItem;
        public void MouseLeftButtonDownOnItem(TreeViewItemEx tvItem, MouseButtonEventArgs e)
        {
            _lastMouseDownTargetItem = tvItem;
            if (IsCtrlPressed || IsShiftPressed || !SelectedItems.Skip(1).Any() || !GetIsItemSelected(tvItem))
            {
                SelectedItemChangedInternal(tvItem);
            }
        }

        public void MouseLeftButtonUpOnItem(TreeViewItemEx tvItem, MouseButtonEventArgs e)
        {
            if (IsCtrlPressed || IsShiftPressed || !SelectedItems.Skip(1).Any())
                return;

            if (!Equals(tvItem, _lastMouseDownTargetItem))
                return;

            SelectedItemChangedInternal(tvItem);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            foreach (var treeViewItem in GetTreeViewItems(this, false))
            {
                var cmdItem = treeViewItem.Item;
                if (cmdItem.IsInEditMode)
                {
                    cmdItem.CommitEdit();
                    e.Handled = true;
                }
            }
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.A && e.IsDown && IsCtrlPressed)
            {
                foreach (var treeViewItem in GetTreeViewItems(this, false))
                {
                    SetIsItemSelected(treeViewItem, true);
                }
                e.Handled = true;
            }
        }
        
        private void SelectedItemChangedInternal(TreeViewItemEx tvItem)
        {
            // Clear all previous selected item states if ctrl is NOT being held down
            if (!IsCtrlPressed)
            {
                foreach (var treeViewItem in GetTreeViewItems(this, true))
                    SetIsItemSelected(treeViewItem, false);
            }
            
            // Is this an item range selection?
            if (IsShiftPressed && _lastItemSelected != null)
            {
                var items = GetTreeViewItemRange(_lastItemSelected, tvItem);
                if (items.Count > 0)
                {
                    foreach (var treeViewItem in items)
                        SetIsItemSelected(treeViewItem, true);

                    //_lastItemSelected = items.Last();
                }
            }
            // Otherwise, individual selection (toggle if CTRL is Pressed)
            else
            {
                SetIsItemSelected(tvItem, !IsCtrlPressed || !GetIsItemSelected(tvItem));
                _lastItemSelected = tvItem;
            }
        }
        private static IEnumerable<TreeViewItemEx> GetTreeViewItems(ItemsControl parentItem, bool includeCollapsedItems, List<TreeViewItemEx> itemList = null)
        {
            if (itemList == null)
                itemList = new List<TreeViewItemEx>();

            for (var index = 0; index < parentItem.Items.Count; index++)
            {
                var tvItem = parentItem.ItemContainerGenerator.ContainerFromIndex(index) as TreeViewItemEx;
                if (tvItem == null) continue;

                yield return tvItem;
                if (includeCollapsedItems || tvItem.IsExpanded)
                {
                    foreach (var item in GetTreeViewItems(tvItem, includeCollapsedItems, itemList))
                        yield return item;
                }
            }
        }
        private List<TreeViewItemEx> GetTreeViewItemRange(TreeViewItemEx start, TreeViewItemEx end)
        {
            var items = GetTreeViewItems(this, false).ToList();

            var startIndex = items.IndexOf(start);
            var endIndex = items.IndexOf(end);
            var rangeStart = startIndex > endIndex || startIndex == -1 ? endIndex : startIndex;
            var rangeCount = startIndex > endIndex ? startIndex - endIndex + 1 : endIndex - startIndex + 1;

            if (startIndex == -1 && endIndex == -1)
                rangeCount = 0;

            else if (startIndex == -1 || endIndex == -1)
                rangeCount = 1;

            return rangeCount > 0 ? items.GetRange(rangeStart, rangeCount) : new List<TreeViewItemEx>();
        }
    }

    public class TreeViewItemEx : TreeViewItem
    {
        private readonly Lazy<FrameworkElement> headerBorder;
        
        public CmdBase Item => DataContext as CmdBase;

        private static bool IsCtrlPressed => (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        private static bool IsShiftPressed => (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
        public TreeViewEx ParentTreeView { get; }

        public int Level
        {
            get { return (int)GetValue(LevelProperty); }
            set { SetValue(LevelProperty, value); }
        }

        protected override DependencyObject GetContainerForItemOverride() => new TreeViewItemEx(ParentTreeView, this.Level+1);
        protected override bool IsItemItsOwnContainerOverride(object item) => item is TreeViewItemEx;

        public TreeViewItemEx(TreeViewEx parentTreeView, int level = 0)
        {
            ParentTreeView = parentTreeView;
            Level = level;

            headerBorder = new Lazy<FrameworkElement>(() => (FrameworkElement)GetTemplateChild("HeaderBorder"));

            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            BindingOperations.ClearBinding(this, TreeViewEx.IsItemSelectedProperty);
            BindingOperations.ClearBinding(this, TreeViewEx.IsItemSelectedProperty);

            if (e.NewValue is CmdBase)
            {
                Binding bind = new Binding
                {
                    Source = e.NewValue,
                    Path = new PropertyPath(nameof(CmdBase.IsSelected)),
                    Mode = BindingMode.TwoWay
                };
                SetBinding(TreeViewEx.IsItemSelectedProperty, bind);
            }

            if (e.NewValue is CmdContainer)
            {
                Binding bind = new Binding
                {
                    Source = e.NewValue,
                    Path = new PropertyPath(nameof(CmdContainer.IsExpanded)),
                    Mode = BindingMode.TwoWay
                };
                SetBinding(IsExpandedProperty, bind);
            }
        }
        
        protected override void OnTextInput(TextCompositionEventArgs e)
        {
            if (IsFocused 
                && Item.IsEditable 
                && !Item.IsInEditMode 
                && !string.IsNullOrEmpty(e.Text)
                && !char.IsControl(e.Text[0]))
            {
                 Item.BeginEdit(initialValue: e.Text);
            }

            base.OnTextInput(e);
        }


        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (IsFocused)
            {
                var selectedItems = ParentTreeView.SelectedItems.ToList();
                if (e.Key == Key.Space && !selectedItems.Any(item => item.IsInEditMode))
                {
                    bool select = selectedItems.All(item => item.IsChecked == false);
                    foreach (var selectedItem in selectedItems)
                    {
                        selectedItem.IsChecked = select;
                    }
                    e.Handled = true;
                }
                else if (e.Key == Key.Return || e.Key == Key.F2)
                {
                    if (Item.IsEditable && !Item.IsInEditMode)
                    {
                        Item.BeginEdit();
                        e.Handled = true;
                    }
                }
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            ParentTreeView.MouseLeftButtonDownOnItem(this, e);

            if (IsFocused 
                && Item.IsEditable 
                && !Item.IsInEditMode 
                && !IsCtrlPressed 
                && ParentTreeView.SelectedItems.Take(2).Count() == 1 
                && (e.ClickCount % 2 == 1 || !(Item is CmdContainer)))
            {
                Item.BeginEdit();
            }
            else
            {
                if (e.ClickCount % 2 == 0 && Item is CmdContainer)
                    IsExpanded = !IsExpanded;

                Focus();
            }
            e.Handled = true;

            base.OnMouseLeftButtonDown(e);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            ParentTreeView.MouseLeftButtonUpOnItem(this, e);
            e.Handled = true;
        }

        protected override void OnIsKeyboardFocusedChanged(DependencyPropertyChangedEventArgs e)
        {
            if ((bool) e.NewValue)
                ParentTreeView.ChangedFocusedItem(this);
        }

        protected override void OnExpanded(RoutedEventArgs e)
        {
            if (Item.IsEditable && Item.IsInEditMode)
                Item.CommitEdit();

            base.OnExpanded(e);
        }

        protected override void OnCollapsed(RoutedEventArgs e)
        {
            if (Item.IsEditable && Item.IsInEditMode)
                Item.CommitEdit();

            if (Item is CmdContainer container)
            {
                // If any child change its state and no other item is selected; select this container
                if (container.SetIsSelectedOnChildren(false) && !ParentTreeView.SelectedTreeViewItems.Any())
                {
                    Item.IsSelected = true;
                }
                else
                {
                    // Give focus to any other selected item
                    ParentTreeView.SelectedTreeViewItems.FirstOrDefault()?.Focus();
                }
            }

            base.OnCollapsed(e);
        }

        static DropTargetAdorner dropTargetAdorner;
        internal static DropTargetAdorner DropTargetAdorner {
            get => dropTargetAdorner;
            set
            {
                dropTargetAdorner?.Detach();
                dropTargetAdorner = value;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var selectedTreeViewItems = ParentTreeView.SelectedTreeViewItems.ToList();
                var set = new HashSet<CmdBase>(selectedTreeViewItems.Select(x => x.Item));
                var data = selectedTreeViewItems.Where(x => !set.Contains(x.Item.Parent));

                var dataObject = new DataObject();

                dataObject.SetData(CmdArgsPackage.ClipboardCmdItemFormat, 123);
                

                DragDrop.DoDragDrop(this, dataObject, DragDropEffects.Move);
            }
        }

        protected override void OnDragEnter(DragEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"DragEnter: {Item.ToString()}");

            if (e.Data.GetDataPresent(CmdArgsPackage.ClipboardCmdItemFormat))
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
                DropTargetAdorner = new DropTargetAdorner(this);
                DropTargetAdorner.MousePosition = e.GetPosition(GetHeaderBorder());
                DropTargetAdorner.InvalidateVisual();
            }
        }
        protected override void OnQueryContinueDrag(QueryContinueDragEventArgs e)
        {
            if (e.Action == DragAction.Cancel || e.EscapePressed)
                DropTargetAdorner = null;

            base.OnQueryContinueDrag(e);
        }
        protected override void OnDragOver(DragEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"DragOver: {Item.ToString()}");

            if (e.Data.GetDataPresent(CmdArgsPackage.ClipboardCmdItemFormat))
            {
                DropTargetAdorner.MousePosition = e.GetPosition(GetHeaderBorder());
                DropTargetAdorner.InvalidateVisual();
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
            }
        }

        protected override void OnDragLeave(DragEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"DragLeave: {Item.ToString()}");

            DropTargetAdorner = null;
        }
        
        protected override void OnDrop(DragEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"OnDrop: {Item.ToString()}");
            DropTargetAdorner = null;
            e.Handled = true;
        }

        public FrameworkElement GetHeaderBorder()
        {
            return headerBorder.Value;
        }

        public static readonly DependencyProperty LevelProperty =
            DependencyProperty.Register(nameof(LevelProperty), typeof(int), typeof(TreeViewItemEx), new PropertyMetadata(0));
    }
}
