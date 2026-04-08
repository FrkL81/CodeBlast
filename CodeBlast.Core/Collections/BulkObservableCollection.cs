using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace CodeBlast.Core.Collections;

public class BulkObservableCollection<T> : ObservableCollection<T>
{
    public BulkObservableCollection() : base() { }
    public BulkObservableCollection(IEnumerable<T> collection) : base(collection) { }
    public BulkObservableCollection(List<T> list) : base(list) { }

    public void AddRange(IEnumerable<T> items)
    {
        CheckReentrancy();
        foreach (var item in items)
            Items.Add(item);

        OnPropertyChanged(new PropertyChangedEventArgs("Count"));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
