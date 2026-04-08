using System.ComponentModel;
using CodeBlast.Core.Services;

namespace CodeBlast.Core.Models;

public class FileNode : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _fullPath = string.Empty;
    private bool _isDirectory;
    private bool _isExcluded;
    private bool _isBinary;
    private long _sizeBytes;
    private int? _tokenCount;
    private CheckState _checkState = CheckState.Unchecked;

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(nameof(Name)); }
    }

    public string FullPath
    {
        get => _fullPath;
        set { _fullPath = value; OnPropertyChanged(nameof(FullPath)); }
    }

    public bool IsDirectory
    {
        get => _isDirectory;
        set { _isDirectory = value; OnPropertyChanged(nameof(IsDirectory)); }
    }

    public bool IsExcluded
    {
        get => _isExcluded;
        set { _isExcluded = value; OnPropertyChanged(nameof(IsExcluded)); }
    }

    public bool IsBinary
    {
        get => _isBinary;
        set { _isBinary = value; OnPropertyChanged(nameof(IsBinary)); }
    }

    public long SizeBytes
    {
        get => _sizeBytes;
        set { _sizeBytes = value; OnPropertyChanged(nameof(SizeBytes)); }
    }

    public int? TokenCount
    {
        get => _tokenCount;
        set { _tokenCount = value; OnPropertyChanged(nameof(TokenCount)); }
    }

    private bool _isUpdating;

    public CheckState CheckState
    {
        get => _checkState;
        set 
        { 
            if (_checkState != value && !_isUpdating)
            {
                _isUpdating = true;
                try
                {
                    _checkState = value; 
                    
                    // 1. Propagar hacia los hijos PRIMERO
                    if (IsDirectory && value != CheckState.Indeterminate)
                    {
                        foreach (var child in Children)
                        {
                            if (!child.IsExcluded && !child.IsBinary)
                                child.CheckState = value;
                        }
                    }

                    // 2. Disparar notificaciones DESPUÉS
                    OnPropertyChanged(nameof(CheckState)); 
                    OnPropertyChanged(nameof(IsChecked)); 
                }
                finally
                {
                    _isUpdating = false;
                }
            }
        }
    }

    /// <summary>Convenience bool binding for CheckBox in the tree view.</summary>
    public bool? IsChecked
    {
        get => _checkState switch
        {
            CheckState.Checked => true,
            CheckState.Unchecked => false,
            _ => null
        };
        set
        {
            CheckState = value == true ? CheckState.Checked : CheckState.Unchecked;
        }
    }

    private bool _needsBinaryVerification;

    public bool NeedsBinaryVerification
    {
        get => _needsBinaryVerification;
        set { _needsBinaryVerification = value; OnPropertyChanged(nameof(NeedsBinaryVerification)); }
    }

    public CodeBlast.Core.Collections.BulkObservableCollection<FileNode> Children { get; set; } = new();

    public FileNode? Parent { get; set; }

    public string RelativePath { get; set; } = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        
        // Bubbling CheckState changes to parent
        if (propertyName == nameof(CheckState) && Parent != null)
        {
            Parent.OnChildCheckStateChanged();
        }
    }

    private bool _isVisible = true;

    public bool IsVisible
    {
        get => _isVisible;
        set { _isVisible = value; OnPropertyChanged(nameof(IsVisible)); }
    }

    private bool _isExpanded;

    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; OnPropertyChanged(nameof(IsExpanded)); }
    }

    public void OnChildCheckStateChanged()
    {
        if (_isUpdating) return;

        // Re-calculate local state based on children
        if (IsDirectory && Children.Count > 0)
        {
            var anyChecked = false;
            var anyUnchecked = false;
            var anyIndeterminate = false;

            foreach (var child in Children)
            {
                if (child.CheckState == CheckState.Checked) anyChecked = true;
                else if (child.CheckState == CheckState.Unchecked) anyUnchecked = true;
                else { anyIndeterminate = true; break; }
            }

            CheckState newState;
            if (anyIndeterminate) newState = CheckState.Indeterminate;
            else if (anyChecked && anyUnchecked) newState = CheckState.Indeterminate;
            else if (anyChecked) newState = CheckState.Checked;
            else newState = CheckState.Unchecked;

            if (_checkState != newState)
            {
                _isUpdating = true;
                try
                {
                    _checkState = newState;
                    OnPropertyChanged(nameof(CheckState));
                    OnPropertyChanged(nameof(IsChecked));
                }
                finally
                {
                    _isUpdating = false;
                }
            }
        }
    }
}
