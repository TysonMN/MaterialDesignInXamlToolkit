using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace MaterialDesignThemes.Wpf
{
    [TemplatePart(Name = PopupPartName, Type = typeof(Popup))]
    [TemplatePart(Name = PopupPartName, Type = typeof(ContentControl))]
    [TemplateVisualState(GroupName = "PopupStates", Name = OpenStateName)]
    [TemplateVisualState(GroupName = "PopupStates", Name = ClosedStateName)]
    public class DialogHost : ContentControl
    {
        public const string PopupPartName = "PART_Popup";
        public const string PopupContentPartName = "PART_PopupContentElement";
        public const string OpenStateName = "Open";
        public const string ClosedStateName = "Closed";

        private ContentControl? _popupContentControl;
        private IInputElement? _restoreFocusDialogClose;

        public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
            nameof(IsOpen), typeof(bool), typeof(DialogHost), new FrameworkPropertyMetadata(default(bool), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, IsOpenPropertyChangedCallback));

        private static void IsOpenPropertyChangedCallback(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
        {
            var dialogHost = (DialogHost)dependencyObject;

            if (dialogHost._popupContentControl != null)
                ValidationAssist.SetSuppress(dialogHost._popupContentControl, !dialogHost.IsOpen);

            if (!dialogHost.IsOpen)
            {
                // Don't attempt to Invoke if _restoreFocusDialogClose hasn't been assigned yet. Can occur
                // if the MainWindow has started up minimized. Even when Show() has been called, this doesn't
                // seem to have been set.
                dialogHost.Dispatcher.InvokeAsync(() => dialogHost._restoreFocusDialogClose?.Focus(), DispatcherPriority.Input);

                return;
            }

            var window = Window.GetWindow(dialogHost);
            dialogHost._restoreFocusDialogClose = FocusManager.GetFocusedElement(window);

            dialogHost.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                CommandManager.InvalidateRequerySuggested();
            }));
        }

        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        public static readonly DependencyProperty DialogContentProperty = DependencyProperty.Register(
            nameof(DialogContent), typeof(object), typeof(DialogHost), new PropertyMetadata(default(object)));

        public object? DialogContent
        {
            get => GetValue(DialogContentProperty);
            set => SetValue(DialogContentProperty, value);
        }

        public override void OnApplyTemplate()
        {
            _popupContentControl = GetTemplateChild(PopupContentPartName) as ContentControl;
            base.OnApplyTemplate();
        }
    }
}