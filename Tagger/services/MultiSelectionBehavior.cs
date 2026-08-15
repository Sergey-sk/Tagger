using Microsoft.Xaml.Behaviors;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace Tagger.viewmodel
{
    public class MultiSelectionBehavior : Behavior<ListBox>
    {
        private bool _isSyncing;

        public static readonly DependencyProperty SelectedItemsProperty =
            DependencyProperty.Register(
                nameof(SelectedItems),
                typeof(IList),
                typeof(MultiSelectionBehavior),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemsChanged));

        public IList SelectedItems
        {
            get => (IList)GetValue(SelectedItemsProperty);
            set => SetValue(SelectedItemsProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();

            AssociatedObject.SelectionChanged += OnListViewSelectionChanged;
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();

            AssociatedObject.SelectionChanged -= OnListViewSelectionChanged;
        }

        // Пользователь выбирает элементы
        private void OnListViewSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(_isSyncing || SelectedItems == null) return;

            _isSyncing = true;

            foreach (var item in e.AddedItems)
            {
                if(!SelectedItems.Contains(item))
                    SelectedItems.Add(item);
            }

            foreach(var item in e.RemovedItems)
            {
                if(SelectedItems.Contains(item))
                    SelectedItems.Remove(item);
            }

            _isSyncing = false;
        }

        //Viewmodel меняет коллекцию элементов
        private static void OnSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if(d is MultiSelectionBehavior behavior)
            {
                if (e.OldValue is INotifyCollectionChanged oldCollection)
                    oldCollection.CollectionChanged -= behavior.OnViewModelCollectionChanged;

                if (e.NewValue is INotifyCollectionChanged newCollection)
                    newCollection.CollectionChanged += behavior.OnViewModelCollectionChanged;

                behavior.SyncListViewWithViewModel();
            }
        }

        private void OnViewModelCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            SyncListViewWithViewModel();
        }

        private void SyncListViewWithViewModel()
        {
            if (_isSyncing || AssociatedObject == null || SelectedItems == null) return;

            _isSyncing = true;

            AssociatedObject.SelectedItems.Clear();

            foreach (var item in SelectedItems)
            {
                AssociatedObject.SelectedItems.Add(item);
            }

            _isSyncing = false;
        }
    }
}
