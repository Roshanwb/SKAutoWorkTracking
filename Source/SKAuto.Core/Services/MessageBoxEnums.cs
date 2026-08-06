namespace SKAuto.Core.Services
{
    /// <summary>
    /// Platform-agnostic message box result.
    /// </summary>
    public enum MessageBoxResultType
    {
        None,
        OK,
        Cancel,
        Yes,
        No
    }

    /// <summary>
    /// Platform-agnostic message box buttons.
    /// </summary>
    public enum MessageBoxButtonType
    {
        OK,
        OKCancel,
        YesNo,
        YesNoCancel
    }

    /// <summary>
    /// Platform-agnostic message box icon.
    /// </summary>
    public enum MessageBoxImageType
    {
        None,
        Information,
        Warning,
        Error,
        Question
    }
}