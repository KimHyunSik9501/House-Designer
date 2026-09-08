using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HouseDesigner.Models;

/// <summary>
/// 캔버스에서 선택하고 이동할 수 있는 모든 도면 요소의 기반 형식입니다.
/// </summary>
public abstract class PlanElement : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
